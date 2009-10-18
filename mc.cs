//
// mc.cs: Main program
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Licensed under the MIT X11 license
//
using System;
using System.Collections;
using System.IO;
using Mono.Terminal;

namespace MouselessCommander {

	public class Shell : Container {
		Panel left, right;
		ButtonBar bar;
		MenuBar menu;
		Label prompt;
		Entry editor;
		
		string [] bar_labels = new string [] {
			"Help", "Menu", "View", "Edit", "Copy", "RenMov", "Mkdir", "Delete", "PullDn", "Quit"
		};

		MenuBarItem [] mc_menu = new MenuBarItem [] {
			new MenuBarItem ("Left", new MenuItem [] {
					new MenuItem ("_Listing Mode...", null, null),
					new MenuItem ("_Sort order...", null, null),
					new MenuItem ("_Filter...", null, null),
					new MenuItem ("_Rescan", "Control-r", null),
				}),
			new MenuBarItem ("File", new MenuItem [] {
					new MenuItem ("_Menu", "F2", null),
					new MenuItem ("_View", "F3", null),
					new MenuItem ("_Edit", "F4", null),
					new MenuItem ("_Copy", "F5", null),
					new MenuItem ("_Rename/Move", "F6", null),
					new MenuItem ("_Make directory", "F7", null),
					new MenuItem ("_Delete", "F8", null),
					null,
					new MenuItem ("_Toggle section", "Control-t", null),
					new MenuItem ("_Select", "Alt-+, +", null),
					new MenuItem ("_Unselect", "Alt-\\, -", null),
					new MenuItem ("_Reverse Selection", "Alt-*, *", null),
					null,
					new MenuItem ("_Quit", "F10", null)
				}),
			new MenuBarItem ("Command", new MenuItem [] {
					new MenuItem ("_Find File", "Alt-?", null),
					new MenuItem ("_Swap panels", "Control-u", null),
					new MenuItem ("Switch _Panels on/off", "Control-o", null),
					new MenuItem ("_Compare Directories", "Control-x d", null),
					new MenuItem ("Show Directory S_izes", "F6", null),
					null,
					new MenuItem ("_Command History", null, null),
					new MenuItem ("_Directory Hotlist", "Control-\\", null),
					null,
					new MenuItem ("_Toggle section", "Control-t", null),
					new MenuItem ("_Select", "Alt-+, +", null),
					new MenuItem ("_Unselect", "Alt-\\, -", null),
					new MenuItem ("_Reverse Selection", "Alt-*, *", null),
					null,
					new MenuItem ("_Quit", "F10", null)
				}),
			new MenuBarItem ("Options", null),
			new MenuBarItem ("Right", new MenuItem [] {
					new MenuItem ("_Listing Mode...", null, null),
					new MenuItem ("_Sort order...", null, null),
					new MenuItem ("_Filter...", null, null),
					new MenuItem ("_Rescan", "C-r", null),
				})
		};

		public override bool ProcessHotKey (int key)
		{
			return base.ProcessHotKey (key);
		}
		
		public Shell () : base (0, 0, Application.Cols, Application.Lines)
		{
			SetupGUI ();
		}

		int PanelHeight ()
		{
			return Application.Lines - 4;
		}
		
		void SetupGUI ()
		{
			var height = PanelHeight ();

			left = Panel.Create ("left", height);
			right = Panel.Create ("right", height);
			bar = new ButtonBar (bar_labels);
			menu = new MenuBar (mc_menu);
			prompt = new Label (0, Application.Lines-2, "bash$ ") {
				Color = Application.ColorBasic
			};
			editor = new Entry (prompt.Text.Length, Application.Lines-2, Application.Cols - prompt.Text.Length, "") {
				Color = Application.ColorBasic,
			};
			
			bar.Action += delegate (int n){
				switch (n){
				case 9:
					menu.Activate (0);
					break;
					
				default:
					break;
				}
			};

			Add (left);
			Add (right);
			Add (bar);
			Add (menu);
			Add (prompt);
			Add (editor);
		}

		static void Main ()
		{
			Application.Init (false);

			Shell s = new Shell ();
			Application.Run (s);
		}
	}
}
