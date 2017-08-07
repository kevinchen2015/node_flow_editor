using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System;
using System.Reflection;

//各种通过编辑器元表配置的定制编辑器菜单开关

namespace NodeFlow
{
    public class NodeFlowEditor : EditorWindow
    {
        [MenuItem("Window/XGame技能编辑器")]
        public static EditorWindow OpenClipEditorEdtior()
        {
            NodeFlowEditorWindow window = GetWindow(typeof(NodeFlowEditorWindow)) as NodeFlowEditorWindow;
            window.name = "XGame技能编辑器";

            NodeFlowEditorSerializeBase ser = new NodeFlowEditorSerializeBase();
            ser.editor = window;
            window.serialize = ser;
            NodeFlowEditorLoader.LoadEditor(window, "XGameSkillEditor.xml");
            window.Show();

            window.OnShow();
            return window;
        }


        [MenuItem("Window/行为树编辑器")]
        public static EditorWindow OpenBTEdtior()
        {
            NodeFlowEditorWindow window = GetWindow(typeof(NodeFlowEditorWindow)) as NodeFlowEditorWindow;
            window.name = "行为树编辑器";

            NodeFlowEditorSerializeBase ser = new NodeFlowEditorSerializeBase();
            ser.editor = window;
            window.serialize = ser;
            NodeFlowEditorLoader.LoadEditor(window, "BTEditor.xml");
            window.Show();

            window.OnShow();
            return window;
        }

    }
  

}
