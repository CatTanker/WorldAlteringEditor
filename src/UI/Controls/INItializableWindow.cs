﻿using Microsoft.Xna.Framework;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TSMapEditor.UI.Controls
{
    /// <summary>
    /// A base class for windows that can create themselves through an INI configuration file.
    /// </summary>
    public class INItializableWindow : EditorWindow
    {
        public INItializableWindow(WindowManager windowManager) : base(windowManager)
        {
        }

        protected IniFile ConfigIni { get; private set; }

        private bool _initialized = false;

        protected bool HasCloseButton { get; set; }

        public T FindChild<T>(string childName, bool optional = false) where T : XNAControl
        {
            T child = FindChild<T>(Children, childName);
            if (child == null && !optional)
                throw new KeyNotFoundException("Could not find required child control: " + childName);

            return child;
        }

        private T FindChild<T>(IEnumerable<XNAControl> list, string controlName) where T : XNAControl
        {
            foreach (XNAControl child in list)
            {
                if (child.Name == controlName)
                    return (T)child;

                XNAControl childOfChild = FindChild<T>(child.Children, controlName);
                if (childOfChild != null)
                    return (T)childOfChild;
            }

            return null;
        }

        public override void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("INItializableWindow cannot be initialized twice.");

            var dsc = Path.DirectorySeparatorChar;
            string configIniPath = Environment.CurrentDirectory + dsc + "Config" + dsc + "UI" + dsc + Name + ".ini";
            
            if (!File.Exists(configIniPath))
                throw new FileNotFoundException("Config INI not found: " + configIniPath);

            ConfigIni = new IniFile(configIniPath);

            Parser.Instance.SetPrimaryControl(this);
            ReadINIForControl(this);
            ReadLateAttributesForControl(this);

            base.Initialize();

            if (HasCloseButton)
            {
                var closeButton = new EditorButton(WindowManager);
                closeButton.Name = "btnCloseX";
                closeButton.Width = Constants.UIButtonHeight;
                closeButton.Height = Constants.UIButtonHeight;
                closeButton.Text = "X";
                closeButton.X = Width - closeButton.Width;
                closeButton.Y = 0;
                AddChild(closeButton);
                closeButton.LeftClick += (s, e) => Hide();
            }

            _initialized = true;
        }

        public override void ParseAttributeFromINI(IniFile iniFile, string key, string value)
        {
            if (key == "HasCloseButton")
                HasCloseButton = iniFile.GetBooleanValue(Name, key, HasCloseButton);
            
            base.ParseAttributeFromINI(iniFile, key, value);
        }

        private void ReadINIForControl(XNAControl control)
        {
            var section = ConfigIni.GetSection(control.Name);
            if (section == null)
                return;

            foreach (var kvp in section.Keys)
            {
                if (kvp.Key.StartsWith("$CC"))
                {
                    var child = CreateChildControl(control, kvp.Value);
                    ReadINIForControl(child);
                    child.Initialize();
                }
                else if (kvp.Key == "$X")
                {
                    control.X = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$Y")
                {
                    control.Y = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$Width")
                {
                    control.Width = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$Height")
                {
                    control.Height = Parser.Instance.GetExprValue(kvp.Value, control);
                }
                else if (kvp.Key == "$TextAnchor" && control is XNALabel)
                {
                    // TODO refactor these to be more object-oriented
                    ((XNALabel)control).TextAnchor = (LabelTextAnchorInfo)Enum.Parse(typeof(LabelTextAnchorInfo), kvp.Value);
                }
                else if (kvp.Key == "$AnchorPoint" && control is XNALabel)
                {
                    string[] parts = kvp.Value.Split(',');
                    if (parts.Length != 2)
                        throw new FormatException("Invalid format for AnchorPoint: " + kvp.Value);
                    ((XNALabel)control).AnchorPoint = new Vector2(Parser.Instance.GetExprValue(parts[0], control), Parser.Instance.GetExprValue(parts[1], control));
                }
                else if (kvp.Key == "$LeftClickAction")
                {
                    if (kvp.Value == "Disable")
                    {
                        control.LeftClick += (s, e) => Hide();
                    }
                }
                else
                {
                    control.ParseAttributeFromINI(ConfigIni, kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Reads a second set of attributes for a control's child controls.
        /// Enables linking controls to controls that are defined after them.
        /// </summary>
        private void ReadLateAttributesForControl(XNAControl control)
        {
            var section = ConfigIni.GetSection(control.Name);
            if (section == null)
                return;

            var children = control.Children.ToList();
            foreach (var child in children)
            {
                var childSection = ConfigIni.GetSection(child.Name);

                if (childSection != null)
                {
                    // This logic should also be enabled for other types in the future,
                    // but it requires changes in XNAUI
                    if (child is XNATextBox)
                    {
                        string nextControl = childSection.GetStringValue("NextControl", null);

                        if (!string.IsNullOrWhiteSpace(nextControl))
                        {
                            var otherChild = children.Find(c => c.Name == nextControl);
                            if (otherChild != null)
                            {
                                ((XNATextBox)child).NextControl = otherChild;

                                if (otherChild is XNATextBox otherAsTb)
                                {
                                    if (otherAsTb.PreviousControl == null)
                                        otherAsTb.PreviousControl = child;
                                }
                            }
                        }

                        string previousControl = childSection.GetStringValue("PreviousControl", null);
                        if (!string.IsNullOrWhiteSpace(previousControl))
                        {
                            var otherChild = children.Find(c => c.Name == previousControl);
                            if (otherChild != null)
                                ((XNATextBox)child).PreviousControl = otherChild;
                        }
                    }
                }
                    
                ReadLateAttributesForControl(child);
            }
        }

        private XNAControl CreateChildControl(XNAControl parent, string keyValue)
        {
            string[] parts = keyValue.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
                throw new INIConfigException("Invalid child control definition " + keyValue);

            if (string.IsNullOrEmpty(parts[0]))
                throw new INIConfigException("Empty name in child control definition for " + parent.Name);

            if (FindChild<XNAControl>(parts[0], true) != null)
            {
                throw new INIConfigException("A control named " + parts[0] + " has been defined more than once.");
            }

            var childControl = EditorGUICreator.Instance.CreateControl(WindowManager, parts[1]);
            childControl.Name = parts[0];
            parent.AddChildWithoutInitialize(childControl);
            return childControl;
        }
    }
}
