using System.Diagnostics;
// 
// ViewParser.cs
// 
// Author:
//   Robert Kozak (rkozak@gmail.com / Twitter:@robertkozak)
// 
// Copyright 2011, Nowcom Corporation.
// 
// Code licensed under the MIT X11 license
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
namespace MonoMobile.Views
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.ComponentModel;
	using System.Drawing;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using MonoTouch.CoreLocation;
	using MonoTouch.UIKit;
	
	public class ViewParser
	{
		private CommandBarButtonItem _LeftFlexibleSpace = new CommandBarButtonItem(UIBarButtonSystemItem.FlexibleSpace) { Location = BarButtonLocation.Left };
		private CommandBarButtonItem _RightFlexibleSpace = new CommandBarButtonItem(UIBarButtonSystemItem.FlexibleSpace) { Location = BarButtonLocation.Right };

		public ViewParser()
		{
		}
		
		public UITableViewSource Parse(DialogViewController controller, object view, MemberInfo member)
		{
			UITableViewSource source = null;
			
			if (view != null)
			{
				view = GetActualView(view);
				controller.RootView = view;

				controller.ToolbarButtons = CheckForToolbarItems(view);
				controller.NavbarButtons = CheckForNavbarItems(view);

				if (member != null)
				{
					var memberData = new MemberData(view, member);
					source = ParseList(controller, view, memberData, null); 
				}
					
				if (source == null)
				{
					source = ParseView(controller, view);
				}

				InitializeSearch(view, source);
			}
			
			return source;
		}
		
		public UITableViewSource ParseView(DialogViewController controller, object view)
		{
			var members = view.GetType().GetMembers(false);

			var sections = new SortedList<int, Section>();
			var memberLists = new SortedList<int, SortedList<int, MemberData>>();
			var sectionIndex = 0;
			var memberOrder = 0;
			
			foreach (var member in members)
			{
				var attributes = member.GetCustomAttributes(false); 

				var memberData = new MemberData(view, member);				
				memberData.Section = sectionIndex;
				
				var pullToRefreshAttribute = member.GetCustomAttribute<PullToRefreshAttribute>();
				if (pullToRefreshAttribute != null)
				{
					((DialogViewController)controller).PullToRefreshCommand = GetCommandForMember(view, member);
					((DialogViewController)controller).RefreshKey = pullToRefreshAttribute.SettingsKey;
					((DialogViewController)controller).EnablePullToRefresh = true;
				}

				var toolbarButtonAttribute = member.GetCustomAttribute<ToolbarButtonAttribute>();
				var navbarButtonAttribute = member.GetCustomAttribute<NavbarButtonAttribute>();
				var skipAttribute = member.GetCustomAttribute<SkipAttribute>();

				if (skipAttribute != null || 
					toolbarButtonAttribute != null || 
					navbarButtonAttribute != null || 	
					pullToRefreshAttribute != null || 
					(attributes.Length == 0 && typeof(MethodInfo) == memberData.Type))
				{
					continue;
				}

				var valueConverterAttribute = member.GetCustomAttribute<ValueConverterAttribute>();
				if (valueConverterAttribute != null)
				{
					memberData.ValueConverter = valueConverterAttribute.ValueConverter;

					memberData.ConverterParameter = valueConverterAttribute.ConverterParameter;
					memberData.ConverterParameterName  = valueConverterAttribute.ConverterParameterPropertyName; 					
				}
				
				var themeAttribute = member.GetCustomAttribute<ThemeAttribute>();
				if (themeAttribute != null)
				{
					var theme = Activator.CreateInstance(themeAttribute.ThemeType) as Theme;
					if (theme != null && theme.CellHeight > 0)
						memberData.RowHeight = theme.CellHeight;
				}

				var rowHeightAttribute = member.GetCustomAttribute<RowHeightAttribute>();
				if (rowHeightAttribute != null)
					memberData.RowHeight = rowHeightAttribute.RowHeight;
				
				var listAttribute = member.GetCustomAttribute<ListAttribute>();
				var isList = (listAttribute != null && listAttribute.DisplayMode == DisplayMode.List) && 
					!typeof(string).IsAssignableFrom(memberData.Type) && 
					(typeof(IEnumerable).IsAssignableFrom(memberData.Type) || 
					typeof(Enum).IsAssignableFrom(memberData.Type));
				
				var orderAttribute = member.GetCustomAttribute<OrderAttribute>();
				if (orderAttribute != null)
				{
					// make sure assigned order is an even number to fit in between the default order 
					// allowing the values int.MinValue and int.MaxValue for the first and Last positions
					memberData.Order = orderAttribute.Order > int.MaxValue / 2 ? int.MaxValue : orderAttribute.Order * 2;
				}
				else
				{				
					// make sure all default memberOrder is odd;
					memberOrder = memberOrder + (memberOrder % 2) + 1;
					memberData.Order = memberOrder;
				}

				var sectionAttribute = member.GetCustomAttribute<SectionAttribute>();
				if (sectionAttribute != null || isList)
				{
					if (sections.Count > 0)
					{
						sectionIndex++;
					}

					memberData.Section = sectionIndex;

					if (sectionAttribute != null && orderAttribute != null)
					{
						memberData.Section = orderAttribute.Order == 0 ? sectionIndex : orderAttribute.Order;
					}
					else
					{
						memberData.Section = sectionIndex;
					}
				}
				
				var viewTypes = GetViewTypes(view, memberData);

				if (!sections.ContainsKey(memberData.Section))
				{
					sections.Add(memberData.Section, CreateSection(controller, memberData, viewTypes));
				}
				else
				{ 
					if (viewTypes != null)
					{
						IList<Type> list = null;
						var key = memberData.Id.ToString();
	
						var viewTypesList = sections[memberData.Section].ViewTypes;
						if (viewTypesList.ContainsKey(key))
						{
							list = viewTypesList[key];
						}
						else
						{
							list = new List<Type>();
							viewTypesList.Add(key, list);
						}
	
						foreach(var viewType in viewTypes)
						{
							if (!list.Contains(viewType))
							{
								list.Add(viewType);
							}	
						}
					}
				}

				if (memberLists.ContainsKey(memberData.Section))
				{
					memberLists[memberData.Section].Add(memberData.Order, memberData); 
				}
				else
				{
					var sortedList = new SortedList<int, MemberData>();
					sortedList.Add(memberData.Order, memberData);
					memberLists.Add(memberData.Section, sortedList);
				}
			}
			
			foreach(var kvp in memberLists)
			{
				var listSources = new SortedList<int, ListSource>();	

				var index = 0;
				var list = kvp.Value.Values.ToList();
				list.ForEach(data => data.Order = index++);

				foreach(var memberData in list)
				{
					var viewTypes = GetViewTypes(view, memberData);

					if ((!typeof(string).IsAssignableFrom(memberData.Type) && typeof(IEnumerable).IsAssignableFrom(memberData.Type)) || typeof(Enum).IsAssignableFrom(memberData.Type))
					{
						var listSource = ParseList(controller, view, memberData, viewTypes) as ListSource; 
			//			listSource.IsRootCell = listSource.IsRootCell || listSources.Count > 0;
			//			listSource.IsNavigable = listSource.IsRootCell;

						listSource.MemberData = memberData;
						listSource.Sections[0].Index = memberData.Section;

						listSources.Add(memberData.Order, listSource);
					}
					else
						listSources.Add(memberData.Order, null);
		

					sections[memberData.Section].ListSources = listSources;
					sections[memberData.Section].Index = memberData.Section;
					if (listSources[0] != null)
						memberData.DataContextBinder = new DataContextBinder(controller, listSources[0].Sections[0]);
				}

				sections[kvp.Key].DataContext = list;
			}
			
			var keyIndex = 0;
			var sectionList = sections.Select(kvp => kvp.Value).ToDictionary((value) => keyIndex++);

			// If there is only one list property return the ListSource rather than create a ViewSource
			if (sectionList.Count == 1 && sectionList[0].DataContext.Count == 1 && sectionList[0].ListSources[0] != null && !sectionList[0].ListSources[0].IsRootCell)
			{
				sectionList[0].ListSources[0].TableViewStyle = UITableViewStyle.Plain;
				return sectionList[0].ListSources[0];
			}

			var source = new ViewSource(controller);
			source.Sections = sectionList;

			return source;
		}

		public UITableViewSource ParseList(DialogViewController controller, object view, MemberData memberData, List<Type> viewTypes)
		{
			object memberValue = memberData.Value;
			var member = memberData.Member;
			var type = memberData.Type;

			var isList = typeof(IEnumerable).IsAssignableFrom(type) || typeof(Enum).IsAssignableFrom(type);
			if (isList)
			{
				var data = type.CreateGenericListFromEnumerable(memberValue);
				var source = new ListSource(controller, (IList)data, viewTypes);

				if (source != null)
				{
					if (type.IsEnum)
					{
						source.SelectedItem = memberValue;
						source.SelectedItems.Add(source.SelectedItem);
					}

					source.Caption = GetCaption(member);

					var listAttribute = member.GetCustomAttribute<ListAttribute>();
					if (listAttribute != null)
					{
						source.DisplayMode = listAttribute.DisplayMode;
						source.SelectionAction = listAttribute.SelectionAction;

						source.IsMultiselect = listAttribute.SelectionAction == SelectionAction.Multiselection;
						source.IsSelectable = source.SelectionAction != SelectionAction.NavigateToView;
						source.IsNavigable = listAttribute.DisplayMode != DisplayMode.Collapsable || listAttribute.SelectionAction == SelectionAction.NavigateToView;

						source.SelectedAccessoryViewType = listAttribute.SelectedAccessoryViewType;
						source.UnselectedAccessoryViewType = listAttribute.UnselectedAccessoryViewType;
						source.UnselectionBehavior = listAttribute.UnselectionBehavior;

						source.ReplaceCaptionWithSelection = listAttribute.ReplaceCaptionWithSelection;
						
						if (!string.IsNullOrEmpty(listAttribute.SelectedItemMemberName)) 
						{
							source.SelectedItemMemberName = listAttribute.SelectedItemMemberName;
						}
						if (!string.IsNullOrEmpty(listAttribute.SelectedItemsMemberName))
						{
							source.SelectedItemsMemberName = listAttribute.SelectedItemsMemberName;
						}
					}						
	
					source.PopOnSelection = source.SelectionAction == SelectionAction.PopOnSelection;
					
					var navigateToViewAttribute = member.GetCustomAttribute<NavigateToViewAttribute>();	
					if (navigateToViewAttribute != null)
					{
						source.IsSelectable = false;
						source.NavigationViewType = navigateToViewAttribute.ViewType;
					}
					
					source.IsRootCell = source.DisplayMode != DisplayMode.List;

					return source;
				}
			}

			return null;
		}
		
	    public static ICommand GetCommandForMember(object view, MemberInfo member)
		{
			string propertyName = string.Empty;
			PropertyInfo propertyInfo = null;
			var commandOption = CommandOption.Disable;

			var buttonAttribute = member.GetCustomAttribute<ButtonAttribute>();
			if (buttonAttribute != null)
			{
				propertyName = buttonAttribute.CanExecutePropertyName;
				commandOption = buttonAttribute.CommandOption;
			}

			var progressAttribute = member.GetCustomAttribute<ProgressAttribute>();
			if (progressAttribute != null)
			{
				propertyName = progressAttribute.CanExecutePropertyName;
				commandOption = progressAttribute.CommandOption;
			}

			var toolbarButtonAttribute = member.GetCustomAttribute<ToolbarButtonAttribute>();
			if (toolbarButtonAttribute != null)
			{
				propertyName = toolbarButtonAttribute.CanExecutePropertyName;
				commandOption = toolbarButtonAttribute.CommandOption;
			}

			var navbarButtonAttribute = member.GetCustomAttribute<NavbarButtonAttribute>();
			if (navbarButtonAttribute != null)
			{
				propertyName = navbarButtonAttribute.CanExecutePropertyName;
				commandOption = navbarButtonAttribute.CommandOption;
			}

			var methodInfo = member as MethodInfo;

			if (methodInfo == null)
				throw new Exception(string.Format("Method not found {0}", member.Name));
			
			object source = view;
			if (!string.IsNullOrEmpty(propertyName))
			{
				PropertyInfo property = source.GetType().GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
				
				if (property == null)
				{
					var dataContext = view as IDataContext<object>;
					if (dataContext != null)
					{
						var vm = dataContext.DataContext;
						if (vm != null)
						{
							source = vm;
							property = source.GetType().GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
						}
					}
				}

				if (property != null)
				{
					if (property.PropertyType == typeof(bool) || property.PropertyType == typeof(bool?))
					{
						propertyInfo = property;
					}
					else
					{
						throw new Exception(string.Format("Property {0} cannot be used for CanExecute property because it does not have a return type of bool", property.Name));
					}
				}
			}

			return new ReflectiveCommand(view, methodInfo, source, propertyInfo) { CommandOption = commandOption };
		}

		public List<CommandBarButtonItem> CheckForToolbarItems(object view)
		{
			var buttonList = new List<CommandBarButtonItem>();
			var members = GetMethods(view);

			foreach(var member in members)
			{
				var buttonAttribute = member.GetCustomAttribute<ToolbarButtonAttribute>();
				var captionAttribute = member.GetCustomAttribute<CaptionAttribute>();

				if (buttonAttribute != null)
				{
					var caption = captionAttribute != null ? captionAttribute.Caption : !buttonAttribute.ButtonType.HasValue && buttonAttribute.ViewType == null ? member.Name.Capitalize() : null;
					
					UIView buttonView = null;
					var title = caption;
					if (buttonAttribute.ViewType != null)
					{	
						buttonView = Activator.CreateInstance(buttonAttribute.ViewType) as UIView;
						
						CheckForInstanceProperties(view, member, buttonView);

						var tappable = buttonView as ITappable;
						if (tappable != null)
						{
							tappable.Command = GetCommandForMember(view, member); 
						}
					}

					var button = CreateCommandBarButton(view, member, title, buttonView, buttonAttribute.Style, buttonAttribute.ButtonType, buttonAttribute.Location);
					
					if (button != null)
					{		
						if (button.Location == BarButtonLocation.Center)
							buttonList.Add(_LeftFlexibleSpace);

						buttonList.Add(button);

						if (button.Location == BarButtonLocation.Center)
							buttonList.Add(_RightFlexibleSpace);
					}
				}
			}
			
			if (buttonList.Count > 0)
			{
				var sortedList = buttonList.OrderBy(button=>button.Order).Distinct().ToList();
				return sortedList;
			}	

			return null;
		}
	
		public List<CommandBarButtonItem> CheckForNavbarItems(object view)
		{
			var buttonList = new List<CommandBarButtonItem>();
			var members = GetMethods(view);
			foreach(var member in members)
			{
				var buttonAttribute = member.GetCustomAttribute<NavbarButtonAttribute>();
				var captionAttribute = member.GetCustomAttribute<CaptionAttribute>();
				var caption = captionAttribute != null ? captionAttribute.Caption : null;
				
				if (buttonAttribute != null)
				{
					var memberName = buttonAttribute.ButtonType.HasValue ? null : member.Name.Capitalize();

					var title = caption ?? memberName;

					if (buttonAttribute.ViewType != null)
					{	
						UIView buttonView = null;
						buttonView = Activator.CreateInstance(buttonAttribute.ViewType) as UIView;
						
						CheckForInstanceProperties(view, member, buttonView);

						var tappable = buttonView as ITappable;
						if (tappable != null)
						{
							tappable.Command = GetCommandForMember(view, member); 
						}
					}

					var button = CreateCommandBarButton(view, member, title, null, buttonAttribute.Style, buttonAttribute.ButtonType, buttonAttribute.Location);
					
					if (button != null)
					{
						buttonList.Add(button);
					}
				}
			}
			
			if (buttonList.Count > 0)
			{
				var sortedList = buttonList.OrderBy(button=>button.Tag).ToList();
				return sortedList;
			}	

			return null;
		}

		public static string GetCaption(MemberInfo member)
		{
			var caption = member.Name;
			var captionAttribute = member.GetCustomAttribute<CaptionAttribute>();

			if (captionAttribute != null)
			{
				caption = captionAttribute.Caption;
			}
			else
			{
				if (caption == "DataContext")
				{
					var propertyInfo = member as PropertyInfo;
					if (propertyInfo != null)
					{
						caption = propertyInfo.PropertyType.Name.Split('.').LastOrDefault();
					}
	
					var fieldInfo = member as FieldInfo;
					if (fieldInfo != null)
					{
						caption = fieldInfo.FieldType.Name.Split('.').LastOrDefault();
					}
				}

				caption = caption.Capitalize();
			}

			return caption;
		}
		
		private Section CreateSection(DialogViewController controller, MemberData memberData, List<Type> viewTypes)
		{
			var listSources = new SortedList<int, ListSource>();
			listSources.Add(memberData.Order, null);

			var memberOrder = 0;
			memberData.Order = memberOrder;

			var sectionMembers = new List<MemberData>();
					
			var section = new Section(controller) { DataContext = sectionMembers };
			
			var sectionAttribute = memberData.Member.GetCustomAttribute<SectionAttribute>();
			if (sectionAttribute != null)
			{
				section.HeaderText = sectionAttribute.Caption;
				section.FooterText = sectionAttribute.Footer;
			}
			section.ViewTypes.Add(memberData.Id.ToString(), viewTypes);
			return section;
		}
		
		private CommandBarButtonItem CreateCommandBarButton(object view, MemberInfo member, string title, UIView buttonView, UIBarButtonItemStyle style, UIBarButtonSystemItem? buttonType, BarButtonLocation location)
		{
			CommandBarButtonItem button = null;

			ICommand command = null;
			var methodInfo = member as MethodInfo;

			if(methodInfo != null)
				command = GetCommandForMember(view, member);

			if (!string.IsNullOrEmpty(title))
			{
				button = new CommandBarButtonItem(title, style, delegate {command.Execute(null); });
			}
			else if (buttonView != null)
			{
				button = new CommandBarButtonItem(buttonView); 
			}
			else
			{
				if (!buttonType.HasValue)
					buttonType = UIBarButtonSystemItem.Done;

				button = new CommandBarButtonItem(buttonType.Value,  delegate { command.Execute(null); });
				button.Style = style;
			}
		
			button.Enabled = true;
			button.Location = location;
			button.Command = command;

			var orderAttribute = member.GetCustomAttribute<OrderAttribute>();
			if (orderAttribute != null)
				button.Order = orderAttribute.Order;
			else 
				button.Order = 0;

			return button;
		}

		private void InitializeSearch(object view, UITableViewSource source)
		{
			var searchbarAttribute = view.GetType().GetCustomAttribute<SearchbarAttribute>();
			var searchbar = source as ISearchBar;
			if (searchbarAttribute != null && searchbar != null)
			{
				searchbar.SearchPlaceholder = searchbarAttribute.Placeholder;
				searchbar.IncrementalSearch = searchbarAttribute.IncrementalSearch;
				searchbar.EnableSearch = searchbarAttribute.ShowImmediately;
				searchbar.IsSearchbarHidden = !searchbarAttribute.ShowImmediately;
		
					
				var methods = GetMethods(view);
				foreach (var method in methods)
				{
					var attribute = method.GetCustomAttribute<SearchbarAttribute>();
					if (attribute != null)
					{
						searchbar.SearchPlaceholder = attribute.Placeholder;
						searchbar.IncrementalSearch = attribute.IncrementalSearch;
						searchbar.EnableSearch = attribute.ShowImmediately;
						searchbar.IsSearchbarHidden = !attribute.ShowImmediately;

						searchbar.SearchCommand = new SearchCommand(view, method as MethodInfo);
						break;
					};
				}
			}
		}
		
		private object GetActualView(object view)
		{
			if (view != null && !(view is IView))
			{
				var type = view.GetType();
				var actualView = ViewContainer.GetExactView(type);
				
				if (actualView == null)
				{
					var viewAttribute = type.GetCustomAttribute<ViewAttribute>();
					if (viewAttribute != null)
					{
						actualView = viewAttribute.ViewType;
					}
				}

				if (actualView != null)
				{
					var newView = Activator.CreateInstance(actualView);
					var dc = newView as IDataContext<object>;
					if (dc != null)
					{
						dc.DataContext = view;
					}

					return newView;
				}
			}

			return view;
		}

		private List<Type> GetViewTypes(object view, MemberData memberData)
		{
			var memberInfo = memberData.Member;
			if (memberInfo != null)
			{
				var viewAttributes = memberInfo.GetCustomAttributes<ViewAttribute>();
				if (viewAttributes.Length > 0)
				{
					var viewTypeList = (from attribute in viewAttributes select attribute.ViewType).ToList();
					var viewAttributesList = viewAttributes.ToList();
					viewAttributesList.ForEach((attribute) => 
					{
						var sizeable = attribute as ISizeable;
						if (sizeable != null)
						{
							memberData.RowHeight = sizeable.GetRowHeight();
						}
					});
					
					return viewTypeList;
				}
			}

			return null;
		}

		private static MemberInfo[] GetMethods(object view)
		{
			return GetMethods(view.GetType()); 
		}

		private static MemberInfo[] GetMethods(Type type)
		{
			return type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Where(m =>
			{
				var methodInfo = m as MethodBase;
				return (methodInfo == null || !methodInfo.IsConstructor && !methodInfo.IsSpecialName);
			}).ToArray();
		}

		private void CheckForInstanceProperties(object view, MemberInfo member, UIView elementView)
		{
			var cellViewTemplate = member.GetCustomAttribute<CellViewTemplate>(true);
			if (cellViewTemplate != null)// && element != _NoElement)
			{
				if (!string.IsNullOrEmpty(cellViewTemplate.InstancePropertyName))
				{
					var instanceProperty = view.GetType().GetProperty(cellViewTemplate.InstancePropertyName, BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
					if (instanceProperty != null)
					{
						UIView instanceView = elementView;
//						if (element != null && element.ElementView != null)
//							instanceView = element.ElementView;

						instanceProperty.SetValue(view, instanceView);
					}
				}

//				if (!string.IsNullOrEmpty(baseControlAttribute.ElementPropertyName))
//				{
//					var elementProperty = view.GetType().GetProperty(baseControlAttribute.ElementPropertyName, BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
//					if (elementProperty != null)
//					{
//						elementProperty.SetValue(view, element);
//					}
//				}
			}
		}
		
		public void HandleCanExecuteChanged(object sender, EventArgs e)
		{
			var reflectiveCommand = sender as ReflectiveCommand;
			if (reflectiveCommand != null)
			{
//				if (reflectiveCommand.CommandOption == CommandOption.Hide)
//				{
//					reflectiveCommand.Element.Visible = reflectiveCommand.CanExecute(null);
//				}
//				else
//				{
//					reflectiveCommand.Element.Enabled = reflectiveCommand.CanExecute(null);
//				}
			}
		}
	}
}