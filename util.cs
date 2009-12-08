//
// util.cs: Utility functions and classes
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using System;
using Mono.Terminal;

namespace MouselessCommander {

	public static class Error {
		[Flags]
		public enum Result {
			Retry = 1, Ignore = 2, Cancel = 4,
			RetryCancel = 5,
			RetryIgnoreCancel = 7
		}

		public static Result Query (Result flags, string errormsg, string condition, string file)
		{
			var s = String.Format (condition, file);
			int len = Math.Min (s.Length, Application.Cols-8);

			var d = new Dialog (len, 8, "Error");
			d.ErrorColors ();
			d.Add (new Label (1, 1, errormsg));
			d.Add (new Label (1, 2, String.Format (condition, file.Ellipsize (len-condition.Length))));

			Result result = Result.Ignore;
			Button b;
			if ((flags & Result.Retry) == Result.Retry){
				b = new Button (0, 0, "Retry", true);
				b.Clicked += delegate {
					result = Result.Retry;
					d.Running = false;
				};
				d.Add (b);
			}
			if ((flags & Result.Ignore) == Result.Ignore){
				b = new Button (0, 0, "Ignore", true);
				b.Clicked += delegate {
					result = Result.Ignore;
					d.Running = false;
				};
				d.Add (b);
			}
			if ((flags & Result.Cancel) == Result.Cancel){
				b = new Button (0, 0, "Cancel", true);
				b.Clicked += delegate {
					result = Result.Cancel;
					d.Running = false;
				};
				d.Add (b);
			}
			Application.Run (d);
			return result;
		}
	}

	public class Progress : Dialog {
		public Progress (string title, int steps) : base (68, 12, title)
		{
		}

		public void Step ()
		{
		}
	}

	
	public static class StringExtensions {
		public static string Ellipsize (this string source, int width)
		{
			if (source.Length <= width)
				return source;
			else
				return source.Substring (0, width/2) + "~" + source.Substring (source.Length - 1 - width/2);
		}
	}
}