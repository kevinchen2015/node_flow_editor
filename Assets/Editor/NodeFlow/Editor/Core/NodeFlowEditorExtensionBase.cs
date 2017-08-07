using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System;
using System.Reflection;

//可扩展的元类型描述和绘制相关的基类 ,可扩展

namespace NodeFlow
{
    public class Param
    {
        public string name = "param0";
        public string type = "string";
        public string desc = "未命名";

        public virtual void Init(ParamProperty property)
        {
            property.name = name;
            property.type = type;
            property.desc = desc;
            property.parmDef = this;
        }

        public virtual void Draw(ParamProperty property)
        {
            if(property.type == "bool")
            {
                if(property.strValue.Length == 0)
                {
                    property.strValue = "false";
                }
                bool b = bool.Parse(property.strValue);
                b = EditorGUILayout.Toggle(property.desc, b);
                property.strValue = b.ToString().ToLower();
            }
            else
            {
                property.strValue = EditorGUILayout.TextField(property.desc, property.strValue);
            }
        }
    }

    public class GlobalEventDef
    {
        public string name;
        public string desc;
        public string type = "int";
        public string value = "0";
        public List<Param> paramList = new List<Param>();  // name , type

        public virtual void Init(EventProperty pro)
        {
            pro.strValue = name;
            pro.eventDef = this;

            pro.param.Clear();
            if (pro.editorParams)
            {
                foreach (Param p in paramList)
                {
                    ParamProperty param = new ParamProperty();
                    p.Init(param);

                    pro.param.Add(param);
                }
            }
        }
 
        public virtual void Draw(EventProperty eventPro)
        {
            if (eventPro.editorParams)
            {
                eventPro.isFoldout = EditorGUILayout.Foldout(eventPro.isFoldout,eventPro.desc + "参数");
                if(eventPro.isFoldout)
                {
                    foreach (ParamProperty p in eventPro.param)
                    {
                        p.parmDef.Draw(p);
                    }
                }
            }
        }
    }

    public class Input
    {
        public string name="input0";
        public string connectType= "Connection";
        public string desc="未命名";
    }
    public class Output
    {
        public string name = "output0";
        public string connectType = "Connection";
        public string desc = "未命名";
    }

    public class EventInfo
    {
        public string name;
        public bool editorParams = false;
        public string desc;

    }

    //for Polymorhism
    public class NodeDef
    {
        public string name = "null_node";
        public string desc = "空节点";
        public string type = "";
        public Color color = Color.black;
        public List<Input>  input = new List<Input>();
        public List<Output> output = new List<Output>();
        public List<Param> param = new List<Param>();
        public List<EventInfo> events = new List<EventInfo>();

        public void MakeDefault()
        {
            input.Add(new Input());
            output.Add(new Output());
            param.Add(new Param());
        }
        
        public virtual void Init(Node node)  
        {
            node.name = this.name;
            node.desc = this.desc;
            node.type = this.type;
            node.color = this.color;
            //input
            foreach (Input o in this.input)
            {
                PreNode preNode = new PreNode(node);
                preNode.inIdx = node.preNodeList.Count;
                preNode.name = o.name;
                preNode.desc = o.desc;
                preNode.connectType = o.connectType;
                preNode.connectDef = NodeFlowEditorWindow.instance.GetConnectionDef(preNode.connectType);
                node.preNodeList.Add(preNode);
            }
            //output
            foreach (Output o in this.output)
            {
                NextNode nextNode = new NextNode(node);
                nextNode.outIdx = node.nextNodeList.Count;
                nextNode.name = o.name;
                nextNode.desc = o.desc;
                nextNode.connectType = o.connectType;
                nextNode.connectDef = NodeFlowEditorWindow.instance.GetConnectionDef(nextNode.connectType);
                node.nextNodeList.Add(nextNode);
            }

            //param
            foreach (Param p in this.param)
            {
                ParamProperty nodePro = new ParamProperty();
                p.Init(nodePro);
                node.property.Add(nodePro);
            }

            //event
            foreach (EventInfo evt  in this.events)
            {
                EventProperty eventPro = new EventProperty();
                eventPro.name = evt.name;
                eventPro.strValue = "";
                eventPro.editorParams = evt.editorParams;
                eventPro.eventDef = null;
                eventPro.desc = evt.desc;
                node.eventProperty.Add(eventPro);
            }
        }

        public virtual Rect DrawNode(Node node)
        {
            GUIContent guic = new GUIContent(node.desc, NodeFlowDrawHelper.CreateTagTex(node.color, node.color, 8, 8));
            Rect rect = GUI.Window(node.id, node.rect, NodeFlowDrawHelper.NodeWindow, guic);
            return rect;
        }

        public virtual void DrawInNodeList()
        {
            Texture2D texture = NodeFlowDrawHelper.CreateTagTex(color, color, 6, 6);
            GUIContent guic = new GUIContent(this.desc, texture);
            if (GUILayout.Button(guic, GUILayout.Width(160), GUILayout.Height(30)))
            {
                OperCmdInfo cmdInfo = new OperCmdInfo();
                cmdInfo.cmd = OperCmd.CREATE_NODE;
                cmdInfo.param = this;
                NodeFlowEditorWindow.instance.AddCmd(cmdInfo);
            }
        }
    }

    public class ConnectionDef
    {
        public string name = "Connection";
        public string desc = "默认链接";
        public bool multiInput = true;
        public bool multiOutput = true;
        public Color color = Color.white;

        public virtual void Init(Connection connect)
        {
            connect.name = name;
            connect.multiInput = multiInput;
            connect.multiOutput = multiOutput;
            connect.color = color;
        }
    }
}
