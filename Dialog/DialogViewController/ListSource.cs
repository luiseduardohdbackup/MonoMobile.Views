// 
//  ListSource.cs
// 
//  Author:
//    Robert Kozak (rkozak@gmail.com / Twitter:@robertkozak)
// 
//  Copyright 2011 - 2012, Nowcom Corporation.
// 
//  Code licensed under the MIT X11 license
// 
//  Permission is hereby granted, free of charge, to any person obtaining
//  a copy of this software and associated documentation files (the
//  "Software"), to deal in the Software without restriction, including
//  without limitation the rights to use, copy, modify, merge, publish,
//  distribute, sublicense, and/or sell copies of the Software, and to
//  permit persons to whom the Software is furnished to do so, subject to
//  the following conditions:
// 
//  The above copyright notice and this permission notice shall be
//  included in all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//  WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
namespace MonoMobile.Views
{	
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using MonoTouch.Foundation;
	using MonoTouch.UIKit;	
	
	[Preserve(AllMembers = true)]
	public class ListSource : BaseDialogViewSource, ISearchBar, IActivation
	{
		private MemberData _MemberData;
		
		private MemberInfo _DataContextSelectedItemsMember;
		private MemberInfo _DataContextSelectedItemMember;		
		private MemberInfo _SelectedItemsMember;
		private MemberInfo _SelectedItemMember;
		
		private UITableViewCellEditingStyle _EditingStyle;
		private object _CanEditSource;
		private MemberInfo _CanEditMember;
		private ICommand _EditCommand;

		public NSIndexPath BaseIndexPath = NSIndexPath.FromRowSection(0, 0);

		public readonly NSString CellId;
		public MemberData MemberData 
		{ 
			get { return _MemberData; }
			set 
			{ 
				if (_MemberData != value)
				{
					_MemberData = value;
					ConfigureRowEditing();
				}
			} 
		}

		public IList SelectedItems { get; set; }
		public object SelectedItem { get; set; }
		
		public string SelectedItemMemberName { set { _SelectedItemMember = GetMemberFromView(value); _DataContextSelectedItemMember = GetMemberFromViewModel(value); } }
		public string SelectedItemsMemberName { set { _SelectedItemsMember = GetMemberFromView(value); _DataContextSelectedItemsMember = GetMemberFromViewModel(value); } }
				
		public UnselectionBehavior UnselectionBehavior { get; set; }
		
		public bool IsMultiselect { get; set; }
		public bool PopOnSelection { get; set; }
		public bool ReplaceCaptionWithSelection { get; set; }
		public DisplayMode DisplayMode { get; set; }

		public SelectionAction SelectionAction { get; set; }
		public bool IsCollapsed { get; set; }
		public IList CollapsedList { get; set; }

		public Type NavigationViewType { get; set; }
		public object NavigationView { get; set; }
		public ListSource NavigationSource { get; set; }
		public bool IsModal {get; set; }
		public UIModalTransitionStyle ModalTransitionStyle { get; set; }

		public ListSource(DialogViewController controller, IList list, IEnumerable<Type> viewTypes) : base(controller)
		{	
			Sections = new Dictionary<int, Section>();
			var section = new Section(controller) { DataContext = list };

			IList<Type> viewTypesList = null;
			if (viewTypes != null)
				viewTypesList = viewTypes.ToList();

			var genericType = list.GetType().GetGenericArguments().FirstOrDefault();
			CellId = new NSString(genericType.ToString());

			section.ViewTypes.Add(CellId, viewTypesList);
			
			Sections.Add(0, section);

			SelectedItems = list.GetType().CreateGenericListFromEnumerable(null);

			CellFactory = new TableCellFactory<UITableViewCell>(CellId);
			

//SelectionDisplayMode = SelectionDisplayMode.Collapsed;
//CollapsedList = new List<object>();
//			foreach(var item in Sections[0].DataContext)
//			{
//				CollapsedList.Add(item);
//			}
//			Sections[0].DataContext.Clear();
//
//IsCollapsed = true;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (MemberData != null)
				{
					MemberData.Dispose();
					MemberData = null;
				}

				if (CellId != null)
				{
					CellId.Dispose();
				}
				
				if (NavigationView != null)
				{
					var disposable = NavigationView as IDisposable;
					if (disposable != null)
					{
						disposable.Dispose();
						NavigationView = null;
					}
				}

				if (NavigationSource != null)
				{
					NavigationSource.Dispose();
					NavigationSource = null;
				}
	
				//TODO: Should this be disposed here??
				foreach(var section in Sections.Values)
				{
					var disposable = section.DataContext as IDisposable;
					if (disposable != null)
					{
						disposable.Dispose();
					}
				}
			}

			base.Dispose(disposing);
		}

		public override int RowsInSection(UITableView tableview, int sectionIndex)
		{
			if (IsRootCell || IsCollapsed)
			{
				return 1;
			}
			
			var numberOfRows = IsCollapsed ? 0 : 0;
			if (Sections.ContainsKey(sectionIndex))
			{
				numberOfRows += Sections[sectionIndex].DataContext.Count;
			}

			return Sections != null ? numberOfRows : 0;
		}

		public override int NumberOfSections(UITableView tableView)
		{
			return Sections != null ? Sections.Count : 0;
		}
	
		public override float GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
		{
			if (MemberData != null && MemberData.RowHeight != 0)
				return MemberData.RowHeight;

			if (RowHeights.Count == 1 && RowHeights[BaseIndexPath] > 0)
			{
				return RowHeights[BaseIndexPath];
			}

			var rowHeight = base.GetHeightForRow(tableView, indexPath);
			return rowHeight;
		}

		public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
		{
			indexPath = NSIndexPath.FromRowSection(indexPath.Row, 0);

			var cell = CellFactory.GetCell(tableView, indexPath, CellId, NibName, (cellId, idxPath) => NewListCell(cellId, idxPath));

			UpdateCell(cell, indexPath);

			return cell;
		}
		
		private UITableViewCell NewListCell(NSString cellId, NSIndexPath indexPath)
		{
			var cellStyle = UITableViewCellStyle.Subtitle;		

			var section = Sections[indexPath.Section];
		
			IList<Type> viewTypes = null;

			var key = cellId.ToString();

			if (section.ViewTypes != null && section.ViewTypes.ContainsKey(key))
			{
				viewTypes = section.ViewTypes[key];
				if (viewTypes != null && !viewTypes.Contains(NavigationViewType))
				{
					viewTypes.Add(NavigationViewType);
				}
			}

			if (viewTypes == null && NavigationViewType != null)
			{
				viewTypes = new List<Type>() { NavigationViewType };
			}
 
			var cell = new ComposableViewListCell(cellStyle, cellId, indexPath, viewTypes, this);
			//If we have a new cell Style then we need to recreate the cell. 
			if (cellStyle != cell.Style)
				cell = new ComposableViewListCell(cell.Style, cellId, indexPath, viewTypes, this);

			return cell;
		}
		
		public override void UpdateCell(UITableViewCell cell, NSIndexPath indexPath)
		{	
			var composableListCell = cell as ComposableViewListCell;
			if (composableListCell != null)
			{
				composableListCell.IndexPath = indexPath;
			}
			
			Type dataType = null;
			var sectionData = GetSectionData(0);
			GetItems();
			
			if (sectionData.Count > 0)
			{
				dataType = sectionData[0].GetType();
			}

			if (DisplayMode != DisplayMode.RootCell)
			{
				if (dataType != null && (dataType.IsPrimitive || dataType.IsEnum) && (SelectionAction == SelectionAction.NavigateToView || SelectionAction == SelectionAction.Custom))
				{
					IsSelectable = false;
					SelectionAction = SelectionAction.Custom;
				}
			}
			else
			{
				if (dataType != null && ((dataType.IsPrimitive || dataType == typeof(string))) && (SelectionAction != SelectionAction.Custom || SelectionAction == SelectionAction.None))
				{
					IsNavigable = sectionData.Count > 1;
					SelectionAction = SelectionAction.Selection;

					if (sectionData.Count == 1)
					{
						SelectedItem = sectionData[0];
						SetItems();
					}
				}
			}

			base.UpdateCell(cell, indexPath);

			cell.SelectionStyle = IsNavigable ? UITableViewCellSelectionStyle.Blue : UITableViewCellSelectionStyle.None;  
			cell.SelectionStyle = IsSelectable ? UITableViewCellSelectionStyle.None : cell.SelectionStyle;  

			cell.SelectionStyle = SelectionAction == SelectionAction.Custom ? UITableViewCellSelectionStyle.Blue : cell.SelectionStyle;
			
			SetSelectionAccessory(cell, indexPath);

			cell.SetNeedsDisplay();
		}

		public override void WillDisplay(UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
		{
			var updated = false;

			var sectionData = GetSectionData(0);
			var section = Sections[0];
			if (section.Views.ContainsKey(cell))
			{
				var views = section.Views[cell];
	
				if (views.Count > 0)
				{
					foreach (var view in views)
					{
						var dc = view as IDataContext<object>;
						if (dc != null)
						{
							dc.DataContext = GetSectionData(0)[indexPath.Row];
						}

						var updateable = view as IUpdateable;
						if (updateable != null)
						{
							updateable.UpdateCell(cell, indexPath);
							cell.SetNeedsDisplay();
							updated = true;
						}
					}
				}
			}
		
			// Do default since no views have done an update
			if (!updated)
			{
				if (IsRootCell)
				{
					cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
					cell.TextLabel.Text = Caption;
					if (IsMultiselect && cell.DetailTextLabel != null)
					{
						cell.DetailTextLabel.Text = SelectedItems.Count.ToString();
					}
					else
					{
						if (SelectedItem != null)
						{
							if (ReplaceCaptionWithSelection)
								cell.TextLabel.Text = SelectedItem.ToString();
							else
								if (cell.DetailTextLabel != null)
									cell.DetailTextLabel.Text = SelectedItem.ToString();
						}
					}
				}
				else
				{
					if (sectionData.Count > 0 && sectionData[indexPath.Row] != null)
					{
						cell.TextLabel.AdjustsFontSizeToFitWidth = true;
						cell.TextLabel.Text = sectionData[indexPath.Row].ToString();
					}
					else
						Console.WriteLine("No Data: for row {0}, section {1}", indexPath.Row, indexPath.Section);
				}
			}
		}

		public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
		{	
			object data = null;

			if (DisplayMode == DisplayMode.Collapsable)
			{
				var section = Sections[0];
				var list = section.DataContext;	
				var indexPaths = new List<NSIndexPath>();
				
				if (IsCollapsed)
				{
					foreach(var item in CollapsedList)
					{
						var row = list.Add(item);
						indexPaths.Add(NSIndexPath.FromRowSection(row, section.Index));
					}
					IsCollapsed  = false;
					CollapsedList.Clear();
					Controller.TableView.InsertRows(indexPaths.ToArray(), UITableViewRowAnimation.Top);
				}
				else
				{
					foreach(var item in list)
					{
						var row = CollapsedList.Add(item);
						indexPaths.Add(NSIndexPath.FromRowSection(row, section.Index));
					}
					IsCollapsed = true;
					list.Clear();
					Controller.TableView.DeleteRows(indexPaths.ToArray(), UITableViewRowAnimation.Top);
				}

			//	Controller.TableView.ReloadRows(new NSIndexPath[] { NSIndexPath.FromRowSection(0, section.Index) }, UITableViewRowAnimation.Fade);
				return;
			}
			if (!IsRootCell)
			{
				SelectedItem = GetSectionData(0)[indexPath.Row]; 

				if (SelectedItems.Contains(SelectedItem))
				{
					SelectedItems.Remove(SelectedItem);

					switch (UnselectionBehavior)
					{
						case UnselectionBehavior.SetSelectedToCurrentValue : break;
						case UnselectionBehavior.SetSelectedToNull : SelectedItem = null; break;
						case UnselectionBehavior.SetSelectedToPreviousValueOrNull :
						{
							if (SelectedItems.Count > 0)
							{
								SelectedItem = SelectedItems[SelectedItems.Count - 1];
							}
							else
							{
								SelectedItem = null;
							}

							break;
						}
					}

				}
				else
				{
					SelectedItems.Add(SelectedItem);
				}

				SetItems();

				if (Controller != null)
				{
					lock(this)
					{
						if (PopOnSelection && !(!IsSelectable || IsNavigable || IsMultiselect))
						{
							new Wait(TimeSpan.FromMilliseconds(0), ()=>    
							{
								Controller.NavigationController.PopViewControllerAnimated(true);
							});
						}
					
						if (IsSelectable || IsMultiselect || !IsNavigable)
						{
							Controller.ReloadData();
						}
						else
						{
							new Wait(TimeSpan.FromMilliseconds(0), () => 
							{
								Controller.ReloadData();
							});
						}
					}
				}

				data = SelectedItem;
			}
			else
				data = GetSectionData(0);
			
			if (SelectionAction == SelectionAction.None)
				return;

			if (SelectionAction == SelectionAction.Custom)
			{
				IsSelectable = false;
				IsMultiselect = false;
				IsNavigable = false;
				Controller.Selected(SelectedItem, indexPath);
				return;
			}

			var dataType = data.GetType();
			if ((dataType.IsPrimitive || dataType.IsEnum) && SelectionAction == SelectionAction.NavigateToView)
			{
				IsSelectable = true;
				SelectionAction = SelectionAction.Selection;
			}

			if (IsNavigable && (data is IEnumerable && (!(data is string))))
			{
				NavigateToList();
				return;
			}			
			
			if (SelectionAction == SelectionAction.NavigateToView && (SelectedItem != null))
			{
				NavigateToView();
			}
		}
		
		public void NavigateToView()
		{
			var viewType = NavigationViewType;
			if (viewType == null)
			{
				viewType = ViewContainer.GetView(SelectedItem.GetType());
			}

			if (viewType != null)
			{
				var disposable = NavigationView as IDisposable;
				if (disposable != null)
				{
					disposable.Dispose();
				}

				NavigationView = Activator.CreateInstance(viewType);
				
				var dc = NavigationView as IDataContext<object>;
				if (dc != null)
				{
					dc.DataContext = SelectedItem;
				}
				else
				{
					NavigationView = SelectedItem;
				}
				
				var initializable = NavigationView as IInitializable;
				if (initializable != null)
				{
					initializable.Initialize();
				}

				Caption = SelectedItem.ToString();
	
				var dvc = new DialogViewController(Caption, NavigationView, Controller.Theme, true);
				Controller.NavigationController.PushViewController(dvc, true);
			}
		}

		public void NavigateToList()
		{
			var section = Sections[0];
			var data = GetSectionData(0);

			if (string.IsNullOrEmpty(Caption))
			{
				Caption = data.ToString();
			}
			
			var dvc = new DialogViewController(Caption, null, Controller.Theme, true) { ToolbarButtons = null, NavbarButtons = null };

			if (NavigationSource == null)
				NavigationSource = new ListSource(dvc, data, section.ViewTypes[CellId]);
			
			NavigationSource.SelectionAction = SelectionAction;

			NavigationSource.IsSelectable = (SelectionAction == SelectionAction.PopOnSelection || SelectionAction == SelectionAction.Selection || SelectionAction == SelectionAction.Multiselection);
			NavigationSource.NavigationViewType = null;
			
			var viewType = NavigationViewType;
			if (viewType == null && SelectionAction == SelectionAction.NavigateToView)
			{
				var genericType = data.GetType().GetGenericArguments().FirstOrDefault();
				viewType = ViewContainer.GetView(genericType);
			}
		
			if (viewType != null)
			{
				NavigationSource.IsNavigable = viewType == typeof(ObjectCellView<object>);
				NavigationSource.NavigationViewType = viewType;
			}

			NavigationSource.IsNavigable = !PopOnSelection && NavigationSource.IsNavigable && SelectionAction != SelectionAction.Custom;

			NavigationSource.CellFactory = CellFactory;

			NavigationSource.SelectedItem = SelectedItem;
			NavigationSource.SelectedItems = SelectedItems;
			NavigationSource.UnselectionBehavior = UnselectionBehavior;

			NavigationSource.IsMultiselect = IsMultiselect;
	//		NavigationSource.IsSelectable = IsSelectable;
	
			if (data.Count > 0 && (data[0].GetType().IsPrimitive || data[0].GetType().IsEnum))
				NavigationSource.IsSelectable = true;

			NavigationSource.PopOnSelection = PopOnSelection;
			NavigationSource.NibName = NibName;

			NavigationSource.TableViewStyle = TableViewStyle;
	
			NavigationSource.IsSearchbarHidden = IsSearchbarHidden;
			NavigationSource.EnableSearch = EnableSearch;
			NavigationSource.IncrementalSearch = IncrementalSearch;
			NavigationSource.SearchPlaceholder = SearchPlaceholder;
			NavigationSource.SearchCommand = SearchCommand;

			NavigationSource.SelectedAccessoryViewType = SelectedAccessoryViewType;
			NavigationSource.UnselectedAccessoryViewType = UnselectedAccessoryViewType;
			
			NavigationSource.MemberData = new MemberData(MemberData.Source, MemberData.Member);

			if (NavigationSource.NavigationViewType != null)
			{
				var rowHeightAttribute = NavigationSource.NavigationViewType.GetCustomAttribute<RowHeightAttribute>();
				if (rowHeightAttribute != null)
				{
					NavigationSource.MemberData.RowHeight = rowHeightAttribute.RowHeight;
				}
			}

			NavigationSource.Controller = dvc;
			dvc.TableView.Source = NavigationSource;
			Controller.NavigationController.PushViewController(dvc, true);
		}

		public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
		{
			return (_EditingStyle != UITableViewCellEditingStyle.None) && (bool)_CanEditMember.GetValue(_CanEditSource);
		}

		public override UITableViewCellEditingStyle EditingStyleForRow(UITableView tableView, NSIndexPath indexPath)
		{
			return _EditingStyle;
		}

		public override void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
		{
			if (editingStyle != UITableViewCellEditingStyle.None)
			{
				if (_EditCommand != null)
				{
					_EditCommand.Execute(indexPath.Row);
				}
			}
		}

		public void Activated()
		{
			GetItems();

			if (NavigationSource != null)
			{
				if (NavigationSource.SelectedItem != null)
					SelectedItem = NavigationSource.SelectedItem;

				if (NavigationSource.SelectedItems != null)
					SelectedItems = NavigationSource.SelectedItems;

				SetItems();

				NavigationSource.SelectedItem = null;
				NavigationSource.SelectedItems = null;
			}
			else
			{
				Controller.UpdateSource();
			}

			Controller.ReloadData();
		}

		public void Deactivated()
		{
		}
		
		private void ConfigureRowEditing()
		{
			if (Controller != null && Controller.RootView != null)
			{
				var cellEditingStyle = Controller.RootView.GetType().GetCustomAttribute<CellEditingStyleAttribute>();
				if (cellEditingStyle == null)
				{
					if (MemberData != null)
					{
						cellEditingStyle = MemberData.Member.GetCustomAttribute<CellEditingStyleAttribute>();
					}
				}
				
				if (cellEditingStyle != null)
				{
					_EditingStyle = cellEditingStyle.EditingStyle;
					_CanEditSource = Controller.RootView;
					_CanEditMember = GetMemberFromView(cellEditingStyle.CanEditMemberName);
					if (_CanEditMember == null)
					{
						_CanEditMember = GetMemberFromViewModel(cellEditingStyle.CanEditMemberName);
					
						var dc = Controller.RootView as IDataContext<object>;
						if (dc != null && dc.DataContext != null)
						{
							_CanEditSource = dc.DataContext;
						}
					}
	
					if (!string.IsNullOrEmpty(cellEditingStyle.EditCommandMemberName))
					{
						var commandMember = _CanEditSource.GetType().GetMember(cellEditingStyle.EditCommandMemberName).FirstOrDefault();
						if (commandMember != null)
						{
							_EditCommand = ViewParser.GetCommandForMember(_CanEditSource, commandMember);
						}
					} 
				}
			}
		}

		private void GetItems()
		{	
			if (_SelectedItemMember != null)
			{
				var item = _SelectedItemMember.GetValue(Controller.RootView);
				SelectedItem = item;
			}
	
			if (_DataContextSelectedItemMember != null)
			{
				var dc = Controller.RootView as IDataContext<object>;
				if (dc != null && dc.DataContext != null)
				{
					var item = _DataContextSelectedItemMember.GetValue(dc.DataContext);
					SelectedItem = item;
				}
			}
	
			if (IsMultiselect)
			{		
				if (_SelectedItemsMember != null)
				{
					var items = _SelectedItemsMember.GetValue(Controller.RootView) as IList;
					SelectedItems = items; 
				}

				if (_DataContextSelectedItemsMember != null)
				{
					//if (_SelectedItemsMember != null)
					{
						var dc = Controller.RootView as IDataContext<object>;
						if (dc != null && dc.DataContext != null)
						{
							var items = _DataContextSelectedItemsMember.GetValue(dc.DataContext) as IList;
							SelectedItems = items;
						}
					}
				}
			}
		}

		private void SetItems()
		{
			if (_SelectedItemMember != null)
			{
				_SelectedItemMember.SetValue(Controller.RootView, SelectedItem);
			}

			if (_DataContextSelectedItemMember != null)
			{
				var dc = Controller.RootView as IDataContext<object>;
				if (dc != null && dc.DataContext != null)
				{			
					_DataContextSelectedItemMember.SetValue(dc.DataContext, SelectedItem);
				}
			}

			if (IsMultiselect)
			{		
				if (_SelectedItemsMember != null)
				{
					_SelectedItemsMember.SetValue(Controller.RootView, SelectedItems);
				}

				if (_DataContextSelectedItemsMember != null)
				{
					var dc = Controller.RootView as IDataContext<object>;
					if (dc != null && dc.DataContext != null)
					{			
						_DataContextSelectedItemsMember.SetValue(dc.DataContext, SelectedItems);
					}
				}

			}
		}

		protected override void SetSelectionAccessory(UITableViewCell cell, NSIndexPath indexPath)
		{
			var sectionData = GetSectionData(0);
			cell.Accessory = SelectionAction == SelectionAction.Custom || SelectionAction == SelectionAction.None ? UITableViewCellAccessory.None : cell.Accessory;
			
			if (SelectionAction != SelectionAction.NavigateToView)
			{
				cell.Accessory = sectionData != null && sectionData.Count > 1 ? cell.Accessory : UITableViewCellAccessory.None;
			}

			base.SetSelectionAccessory(cell, indexPath);
			
			if (IsSelectable && SelectionAction != SelectionAction.None)
			{	
				var selectedIndex = sectionData.IndexOf(SelectedItem);
				UIView selectedAccessoryView = SelectedAccessoryViews.Count > 0 ? SelectedAccessoryViews[cell] : null;
				UIView unselectedAccessoryView = UnselectedAccessoryViews.Count > 0 ? UnselectedAccessoryViews[cell] : null;

				if (selectedAccessoryView != null)
				{
					cell.AccessoryView = selectedIndex == indexPath.Row ? selectedAccessoryView : unselectedAccessoryView;
				}
				else
				{
					cell.Accessory = selectedIndex == indexPath.Row ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;
				}
				
				if (IsMultiselect && SelectedItems != null)
				{
					if (!SelectedItems.Contains(SelectedItem))
					{
						cell.AccessoryView = null;
						cell.Accessory = UITableViewCellAccessory.None;
					}
					
					foreach (var item in SelectedItems)
					{
						selectedIndex = sectionData.IndexOf(item);
						
						if (selectedIndex != indexPath.Row) continue;

						if (selectedAccessoryView != null)
						{
							cell.AccessoryView = selectedAccessoryView;
						}
						else
							cell.Accessory = UITableViewCellAccessory.Checkmark;
					}
				}
			}
		}
	}
}
