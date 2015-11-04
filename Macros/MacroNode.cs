﻿//   MacroNode.cs
//
//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri
//
// This work is licensed under the Creative Commons Attribution 4.0 International License. 
// To view a copy of this license, visit http://creativecommons.org/licenses/by/4.0/ 
// or send a letter to Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.

using System;
using UnityEngine;

namespace ThrottleControlledAvionics
{
	public class MacroNode : TypedConfigNodeObject
	{
		public delegate void Selector(Action<MacroNode> callback);
		protected static TCAGlobals GLB { get { return TCAScenario.Globals; } }
		protected VesselConfig CFG;

		public MacroNode Parent;
		[Persistent] public string Name;
		[Persistent] public bool Active;
		[Persistent] public bool Done;

		public virtual bool Edit { get; set; }

		public Selector SelectNode;
		public Condition.Selector SelectCondition;

		public MacroNode()
		{ Name = Utils.ParseCamelCase(GetType().Name.Replace(typeof(MacroNode).Name, "")); }

		public virtual void OnChildRemove(MacroNode child) 
		{ child.Parent = null; }

		public virtual bool AddChild(MacroNode child) 
		{ return false; }

		public virtual bool AddSibling(MacroNode sibling) 
		{ return false; }

		public virtual void OnChildActivate(MacroNode child)
		{ if(Parent != null) Parent.OnChildActivate(child); }

		protected virtual void DrawDeleteButton()
		{
			if(Parent != null && Parent.Edit &&
			   GUILayout.Button("X", 
			                    Styles.red_button, 
			                    GUILayout.Width(20)))
				Parent.OnChildRemove(this);
		}

		protected virtual void DrawThis() 
		{ GUILayout.Label(Name, Active? Styles.green_button : Styles.normal_button, GUILayout.ExpandWidth(true)); }

		protected virtual void CleanUp() {}

		public virtual void Draw() 
		{ 
			GUILayout.BeginHorizontal();
			DrawDeleteButton();
			DrawThis();
			GUILayout.EndHorizontal();
			CleanUp();
		}

		/// <summary>
		/// Perform the Action on a specified VSL.
		/// Returns true if it is not finished and should be called on next update. 
		/// False if it is finished.
		/// </summary>
		/// <param name="VSL">VesselWrapper</param>
		protected virtual bool Action(VesselWrapper VSL) { return false; }

		public bool Execute(VesselWrapper VSL)
		{
			if(Done) return false;
			if(!Active)
			{
				Active = true;
				if(Parent != null) 
					Parent.OnChildActivate(this);
			}
			Done = !Action(VSL);
			Active &= !Done;
			return !Done;
		}

		public virtual void Rewind() { Active = Done = false; }

		public void CopyFrom(MacroNode mn)
		{
			var node = new ConfigNode();
			mn.Save(node);
			Load(node);
		}

		public MacroNode GetCopy()
		{
			var constInfo = GetType().GetConstructor(Type.EmptyTypes);
			if(constInfo == null) return null;
			var mn = (MacroNode)constInfo.Invoke(null);
			mn.CopyFrom(this);
			mn.Rewind();
			return mn;
		}

		public virtual void SetSelector(Selector selector) 
		{ SelectNode = selector; }

		public virtual void SetConditionSelector(Condition.Selector selector) 
		{ SelectCondition = selector; }

		public virtual void SetCFG(VesselConfig cfg) 
		{ CFG = cfg; }
	}
}

