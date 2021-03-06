// 
// MonoMobileAppDelegate.cs
// 
// Author:
//   Robert Kozak (rkozak@gmail.com / Twitter:@robertkozak)
// 
// Copyright 2011 - 2012, Nowcom Corporation.
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
	using System.Collections.Generic;
	using System.Linq;
	using MonoTouch.Foundation;
	using MonoTouch.UIKit;

	[Register("MonoMobileAppDelegate")]
	public class MonoMobileAppDelegate : UIApplicationDelegate
	{		
		// This method is invoked when the application has loaded its UI and its ready to run
		public override bool FinishedLaunching(UIApplication app, NSDictionary options)
		{	
			//HttpDebug.Start (); 

			MonoMobileApplication.NavigationController = new NavigationController();
			MonoMobileApplication.Window = new UIWindow(UIScreen.MainScreen.Bounds);

			MonoMobileApplication.Window.AddSubview(MonoMobileApplication.NavigationController.View);
			MonoMobileApplication.Window.MakeKeyAndVisible();

			MonoMobileApplication.NavigationController.View.Alpha = 0.0f;
	
#if DEBUG
			var thread = new System.Threading.Thread(() =>
			{
				using (NSAutoreleasePool pool = new NSAutoreleasePool())
				{
					InvokeOnMainThread(()=> { Startup(); });
				}
			});
			
			thread.Start();
#else
			InvokeOnMainThread(()=> { Startup(); });
#endif
			return true;
		}

		private static void Startup()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach(var assembly in assemblies)
			{
				var types = assembly.GetTypes();
				foreach(var type in types)
				{
					var initialize = type.GetCustomAttribute<InitializeAttribute>();
					if (initialize != null)
					{
						var initializeMethod = type.GetMethod("Initialize");
						if (initializeMethod != null)
						{
							initializeMethod.Invoke(type, null);
						}
					}
				}
			}
 
			MonoMobileApplication.Views = new List<object>();
			foreach (var viewType in MonoMobileApplication.ViewTypes)
			{
				var view = Activator.CreateInstance(viewType);
				MonoMobileApplication.Views.Add(view);
			}

			foreach (var view in MonoMobileApplication.Views)
			{				
				var initalizable = view as IInitializable;
				if (initalizable != null)
				{
					initalizable.Initialize();
				}
			}

			foreach(var view in MonoMobileApplication.Views)
			{	
				MonoMobileApplication.DialogViewControllers.Add(new DialogViewController(MonoMobileApplication.Title, view, Theme.CreateTheme(), true) { Autorotate = true } );
			}
	
			MonoMobileApplication.NavigationController.ViewControllers = MonoMobileApplication.DialogViewControllers.ToArray();

			UIView.BeginAnimations("fadeIn");
			UIView.SetAnimationDuration(0.3f);
			MonoMobileApplication.NavigationController.View.Alpha = 1.0f;
			UIView.CommitAnimations();
		}

		public override void WillEnterForeground(UIApplication application)
		{
			if (MonoMobileApplication.ResumeFromBackgroundAction != null)
				MonoMobileApplication.ResumeFromBackgroundAction();
		}
		
		public override void ReceiveMemoryWarning(UIApplication application)
		{
			Console.WriteLine("Memory warning.");
		}

        // This method is allegedly required in iPhoneOS 3.0
        public override void OnActivated(UIApplication application)
        {
        }

		public override void ReceivedLocalNotification(UIApplication application, UILocalNotification notification)
		{
			if (MonoMobileApplication.ReceivedLocalNoticiationAction != null)
				MonoMobileApplication.ReceivedLocalNoticiationAction(notification);
		}

		public override void DidEnterBackground(UIApplication application)
		{
			if (MonoMobileApplication.DidEnterBackgroundAction != null)
			{
				MonoMobileApplication.DidEnterBackgroundAction(application);
			}
		}
	}
}

