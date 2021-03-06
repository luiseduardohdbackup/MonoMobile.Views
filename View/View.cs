//
// View.cs: Base class for MVVM Views
//
// Author:
//   Robert Kozak (rkozak@gmail.com / Twitter:@robertkozak
//
// Copyright 2011, Nowcom Corporation
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
	using MonoTouch.UIKit;	

	public abstract class View : UIView, IView, IDataContext<object>, IInitializable, ICaption, IThemeable
	{
		private object _DataContext;
		
		public UITableView TableView { get; set; }

		public string Caption { get; set; }

		public object DataContext 
		{ 
			get { return GetDataContext(); }
			set { SetDataContext(value); }
		}
		
		protected virtual object GetDataContext()
		{
			return _DataContext;
		}

		protected virtual void SetDataContext(object dataContext)
		{
			if (_DataContext != dataContext)
			{
				_DataContext = dataContext;
			}
		}

		public View(): this(null)
		{
		}

		public View(string caption)
		{
			Caption = caption;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				var dc = this as IDataContext<MemberData>;
				if (dc != null)
				{
					dc.DataContext.Dispose();
				}

				if (Theme != null)
				{
					Theme.Dispose();
					Theme = null;
				}
			}

			base.Dispose(disposing);
		}

		public override string ToString()
		{
			return null;
		}
	
		public virtual void Initialize()
		{
		}
		
		public virtual void Reload()
		{
			MonoMobileApplication.CurrentDialogViewController.TableView.ReloadData();
		}

		#region IThemeable implementation
		public void InitializeTheme(UITableViewCell cell)
		{
		}

		public void ApplyTheme(UITableViewCell cell)
		{
		}

		public Theme Theme { get; set; }
		#endregion
	}
}

