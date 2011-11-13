// 
//  {filename}.cs
// 
//  Author:
//    Robert Kozak (rkozak@gmail.com / Twitter:@robertkozak)
// 
//  Copyright 2011, Nowcom Corporation.
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
	using System.Collections.Generic;
	using System.Reflection;
	using MonoTouch.UIKit;

	public static class ViewContainer
	{
		private static Dictionary<Type, Type> _TypeViewMap = new Dictionary<Type, Type>();

		public static void RegisterView(Type type, Type viewType)
		{
			if (!typeof(UIView).IsAssignableFrom(viewType))
			{
				throw new Exception(string.Format("{0} must be a descendent of UIView", viewType.ToString()));
			}
			
			if (_TypeViewMap.ContainsKey(type))
			{
				_TypeViewMap[type] = viewType;
			}
			else
				_TypeViewMap.Add(type, viewType);
		}
		
		public static Type GetExactView(Type type)
		{
			if (type != null)
			{
				if (_TypeViewMap.ContainsKey(type))
				{
					return _TypeViewMap[type];
				}
			}

			return null;
		}

		public static Type GetView(Type type)
		{
			if (type == null)
				return null;

			if (_TypeViewMap.ContainsKey(type))
			{
				return _TypeViewMap[type];
			}
			else
			{
				var interfaces = type.GetInterfaces();
				foreach(var i in interfaces)
				{
					if (_TypeViewMap.ContainsKey(i))
					{
						return _TypeViewMap[i];
					}
				}
			}
			
			return GetView(type.BaseType);
		}

		public static Type GetView(MemberData memberData)
		{
			var attributes = memberData.Member.GetCustomAttributes(false);

			foreach(Attribute attribute in attributes)
			{
				var attributeType = attribute.GetType();

				if (_TypeViewMap.ContainsKey(attributeType))
				{
					return _TypeViewMap[attributeType];
				}
			}
			
			var type = memberData.Member.GetMemberType();

			return GetView(type);
		}
	}
}
