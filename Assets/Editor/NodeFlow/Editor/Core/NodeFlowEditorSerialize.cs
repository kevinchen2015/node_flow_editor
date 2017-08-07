using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System;
using System.Reflection;
using System.Diagnostics;


//序列化xml版本

namespace NodeFlow
{
    public class NodeFlowEditorSerializeBase
    {
        //---------------------editor meta-------------------------------------
        public void LoadMeataEditor(string path)
        {
            EditorMetaLoader editorMetaLoader = new EditorMetaLoader();
            editorMetaLoader.LoadEditor(editor, path);
        }

        //------------------------------save flow------------------------------------------
        public NodeFlowEditorWindow editor;
        public virtual void SaveToXml(NodeFlowEditorWindow editor,string path, NodeFlow flow)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("editor");
            doc.AppendChild(root);

            root.SetAttribute("name", editor.edtorName);
            root.SetAttribute("version", editor.version);
            SerializeFlow(root, flow);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new System.Text.UTF8Encoding(false);
            settings.Indent = true;

            XmlWriter xmlWriter = XmlWriter.Create(path, settings);
            doc.Save(xmlWriter);
            xmlWriter.Flush();
            xmlWriter.Close();
            doc = null;
            xmlWriter = null;
        }

        protected virtual void SerializeFlow(XmlElement root,NodeFlow flow)
        {
            //node
            {
                Dictionary<int, Node> nodes = flow.GetNodes();
                foreach (KeyValuePair<int,Node> kv in nodes )
                {
                    SerializeNode(root,kv.Value);
                }
            }

            //connection
            {
                List < Connection > connects = flow.GetConnections();
                foreach (Connection connect in connects)
                {
                    SerializeConnect(root, connect,flow);
                }
            }
        }

        protected virtual void SerializeConnect(XmlElement root, Connection connect, NodeFlow flow)
        {
            XmlElement connectElm = root.OwnerDocument.CreateElement("connection");
            root.AppendChild(connectElm);

            connectElm.SetAttribute("name", connect.name);
            connectElm.SetAttribute("from_node", connect.outId.ToString());
            Node node = flow.GetNodeByID(connect.outId);
            connectElm.SetAttribute("from_output", node.GetNextNodeNameByIdx(connect.outIdx));
            connectElm.SetAttribute("to_node", connect.inId.ToString());
            node = flow.GetNodeByID(connect.inId);
            connectElm.SetAttribute("to_input", node.GetPreNodeNameByIdx(connect.inIndex));
        }

        protected virtual void SerializeNode(XmlElement root, Node node)
        {
            XmlElement nodeElm = root.OwnerDocument.CreateElement("node");
            root.AppendChild(nodeElm);
            nodeElm.SetAttribute("name", node.name);
            nodeElm.SetAttribute("id", node.id.ToString());
            nodeElm.SetAttribute("pos_x", node.rect.x.ToString());
            nodeElm.SetAttribute("pos_y", node.rect.y.ToString());
            //input
            {
                foreach (PreNode preNode in node.preNodeList)
                {
                    XmlElement inputElm = root.OwnerDocument.CreateElement("input");
                    nodeElm.AppendChild(inputElm);
                    inputElm.SetAttribute("name",preNode.name);
                }
            }
            //output
            {
                foreach (NextNode nextNode in node.nextNodeList)
                {
                    XmlElement output = root.OwnerDocument.CreateElement("output");
                    nodeElm.AppendChild(output);
                    output.SetAttribute("name", nextNode.name);  
                }
            }
            //param
            {
                foreach (ParamProperty pro in node.property)
                {
                    XmlElement param = root.OwnerDocument.CreateElement("param");
                    nodeElm.AppendChild(param);
                    param.SetAttribute("name", pro.name);
                    param.SetAttribute("type", pro.type);
                    param.SetAttribute("value", pro.strValue);
                }
            }
            //event
            {
                foreach (EventProperty evt in node.eventProperty)
                {
                    XmlElement evtElm = root.OwnerDocument.CreateElement("event");
                    nodeElm.AppendChild(evtElm);
                    evtElm.SetAttribute("name", evt.name);
                    evtElm.SetAttribute("value", evt.strValue);
                    if(evt.editorParams)
                    {
                        foreach (ParamProperty p in evt.param)
                        {
                            XmlElement param = root.OwnerDocument.CreateElement("event_param");
                            nodeElm.AppendChild(param);

                            param.SetAttribute("name", p.name);
                            param.SetAttribute("type", p.type);
                            param.SetAttribute("value", p.strValue);
                            param.SetAttribute("event", evt.name);
                        }
                    }
                }
            }
        }
      
        //----------------------------load flow----------------------------------
        public virtual NodeFlow ParseFlow(string path)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlNode editorNode = doc.SelectSingleNode("editor");
            if (editorNode == null)
            {
                UnityEngine.Debug.LogError("xml config load error! " + path);
                return null;
            }
            if (!editorNode.HasChildNodes)
            {
                return null;
            }
            NodeFlow flow = new NodeFlow();
            //1 node
            XmlNode cfgNode = editorNode.FirstChild;
            while (cfgNode != null) 
            {
                XmlElement cfgElm = cfgNode as XmlElement;
                string cfgElmName = cfgElm.Name;
                if (cfgElmName.Equals("node"))
                {
                    ParseNode(cfgElm, flow);
                }
                else if (cfgElmName.Equals("connection"))
                {
                }
                else
                {
                    UnityEngine.Debug.LogError("can not parse element :" + cfgElmName);
                }
                cfgNode = cfgNode.NextSibling;
            } 
            //2 connection
            cfgNode = editorNode.FirstChild;
            while (cfgNode != null) 
            {
                XmlElement cfgElm = cfgNode as XmlElement;
                string cfgElmName = cfgElm.Name;
                if (cfgElmName.Equals("connection"))
                {
                    ParseConnection(cfgElm, flow);
                }
                cfgNode = cfgNode.NextSibling;
            } 
            doc = null;
            return flow;
        }

        protected virtual void ParseNode(XmlElement elm, NodeFlow flow)
        {
            string defName = elm.GetAttribute("name");
            NodeDef def = editor.GetNodeDef(defName);
            if(def == null)
            {
                UnityEngine.Debug.LogError("can not prase node:"+defName);
                return;
            }
            int id = int.Parse(elm.GetAttribute("id"));
            Vector2 pos = new Vector2(100, 100);
            pos.x = float.Parse(elm.GetAttribute("pos_x"));
            pos.y = float.Parse(elm.GetAttribute("pos_y"));
            Node node = flow.CreateNode(def, pos, id);
            //parse param
            XmlNode paramNode = elm.FirstChild;
            while (paramNode != null) 
            {
                XmlElement cfgElm = paramNode as XmlElement;
                string cfgElmName = cfgElm.Name;
                if (cfgElmName.Equals("param"))
                {
                    ParseParamValue(cfgElm, node);
                }
                else if(cfgElmName.Equals("event"))
                {
                    ParseEventValue(cfgElm, node);
                }
                paramNode = paramNode.NextSibling;
            }
            paramNode = elm.FirstChild;
            while (paramNode != null)
            {
                XmlElement cfgElm = paramNode as XmlElement;
                string cfgElmName = cfgElm.Name;
                if (cfgElmName.Equals("event_param"))
                {
                    ParseEventParamValue(cfgElm, node);
                }
                paramNode = paramNode.NextSibling;
            } 
        }

        protected virtual void ParseEventValue(XmlElement elm, Node node)
        {
            string evtName = elm.GetAttribute("name");
            EventProperty evtPro = node.GetEventProperty(evtName);
            if(evtPro == null)
            {
                UnityEngine.Debug.LogError("can not find event:"+ evtName);
                return;
            }
            evtPro.strValue = elm.GetAttribute("value");
            evtPro.param.Clear();
            GlobalEventDef evtDef = editor.GetGlobalEventDef(evtPro.strValue);
            if (evtDef == null)
            {
                UnityEngine.Debug.LogError("can not find event def:" + evtPro.strValue);
                return;
            }
            evtDef.Init(evtPro);
            for(int i = 0;i < editor.globalEventDef.Count;++i)
            {
                if(editor.globalEventDef[i].name == evtPro.strValue)
                {
                    evtPro.selectIdx = i;
                    return;
                }
            }
        }

        protected virtual void ParseEventParamValue(XmlElement elm, Node node)
        {
            string evtName = elm.GetAttribute("event");
            EventProperty evtPro = node.GetEventProperty(evtName);
            if (evtPro == null)
            {
                UnityEngine.Debug.LogError("can not find event:" + evtName);
                return;
            }
            string name = elm.GetAttribute("name");
            string type = elm.GetAttribute("type");
            foreach (ParamProperty pro in evtPro.param)
            {
                if (pro.name == name && pro.type == type)
                {
                    pro.strValue = elm.GetAttribute("value");
                    return;
                }
            }
            UnityEngine.Debug.LogError("ParseParamValue() error!");
        }

        protected virtual void ParseParamValue(XmlElement elm, Node node)
        {
            string name = elm.GetAttribute("name");
            string type = elm.GetAttribute("type");
            foreach (ParamProperty pro in node.property)
            {
                if(pro.name == name && pro.type == type)
                {
                    pro.strValue = elm.GetAttribute("value");
                    return;
                }
            }
            UnityEngine.Debug.LogError("ParseParamValue() error!");
        }

        protected virtual void ParseConnection(XmlElement elm, NodeFlow flow)
        {
            string connectName = elm.GetAttribute("name");
            ConnectionDef def = editor.GetConnectionDef(connectName);
            if (def == null)
            {
                UnityEngine.Debug.LogError("can not prase connect:" + connectName);
                return;
            }
            int fromNode = int.Parse(elm.GetAttribute("from_node"));
            string fromOutput = elm.GetAttribute("from_output");
            Node outNode = flow.GetNodeByID(fromNode);
            int fromOutputIdx = outNode.GetNextNodeIdxByName(fromOutput);
            NextNode nextNode = outNode.nextNodeList[fromOutputIdx];

            int toNode = int.Parse(elm.GetAttribute("to_node"));
            string toInput = elm.GetAttribute("to_input");
            Node inNode = flow.GetNodeByID(toNode);
            int toInputIdx = inNode.GetPreNodeIdxByName(toInput);
            PreNode preNode = inNode.preNodeList[toInputIdx];

            flow.BindConnection(nextNode, preNode, def);
        }
    }


    public class EditorMetaLoader
    {
        public NodeFlowEditorWindow editor;
        public virtual void LoadEditor(NodeFlowEditorWindow _editor, string path)
        {
            editor = _editor;
            editor.nodeDef.Clear();
            NodeDef nodeDef = new NodeDef();
            nodeDef.MakeDefault();
            editor.nodeDef.Add(nodeDef);
            editor.connectDef.Clear();
            editor.connectDef.Add(new ConnectionDef());
            editor.globalEventDef.Clear();
            //editor.variableDef.Clear();

            XmlDocument doc = new XmlDocument();
            path = Application.dataPath + "/Editor/NodeFlow/Editor/" + path;
            doc.Load(path);
            XmlNode editorNode = doc.SelectSingleNode("editor");

            if (editorNode == null)
            {
                UnityEngine.Debug.LogError("editor config load error! " + path);
                goto __end;
            }

            editor.edtorName = (editorNode as XmlElement).GetAttribute("name");
            editor.version = (editorNode as XmlElement).GetAttribute("version");

            if (!editorNode.HasChildNodes)
            {
                goto __end;
            }

            XmlNode cfgNode = editorNode.FirstChild;
            while (cfgNode != null)
            {
                XmlElement cfgElm = cfgNode as XmlElement;
                string cfgElmName = cfgElm.Name;
                if (cfgElmName.Equals("node_def"))
                {
                    ParseNodeDef(cfgElm);
                }
                else if (cfgElmName.Equals("connection_def"))
                {
                    ParseConnectDef(cfgElm);
                }
                else if (cfgElmName.Equals("event_def"))
                {
                    ParseEventDef(cfgElm);
                }
                else
                {
                    UnityEngine.Debug.LogError("can not parse element :" + cfgElmName);
                }
                cfgNode = cfgNode.NextSibling;
            }
            __end:
            editor.OnEditorMetaLoaded();
        }

        protected virtual void ParseNodeDef(XmlElement elm)
        {
            NodeDef nodeDef = new NodeDef();
            nodeDef.name = elm.GetAttribute("name");
            nodeDef.desc = elm.GetAttribute("desc");
            if (elm.HasAttribute("type"))
                nodeDef.type = elm.GetAttribute("type");

            if (elm.HasAttribute("color"))
            {
                string strColor = elm.GetAttribute("color");
                string[] rgb = strColor.Split(',');
                Vector3 vColor = new Vector3(float.Parse(rgb[0]), float.Parse(rgb[1]), float.Parse(rgb[2]));
                vColor /= 255.0f;
                nodeDef.color = new Color(vColor.x, vColor.y, vColor.z);
            }

            editor.nodeDef.Add(nodeDef);
            if (!elm.HasChildNodes)
            {
                return;
            }

            XmlNode cfgNode = elm.FirstChild;
            while (cfgNode != null)
            {
                XmlElement cfgElm = cfgNode as XmlElement;
                string cfgElmName = cfgElm.Name;
                if (cfgElmName.Equals("input"))
                {
                    ParseNodeInput(nodeDef, cfgElm);
                }
                else if (cfgElmName.Equals("output"))
                {
                    ParseNodeOutput(nodeDef, cfgElm);
                }
                else if (cfgElmName.Equals("param"))
                {
                    ParseNodeParam(nodeDef, cfgElm);
                }
                else if(cfgElmName.Equals("event"))
                {
                    ParseNodeEvent(nodeDef, cfgElm);
                }
                else
                {
                    UnityEngine.Debug.LogError("can not parse element :" + cfgElmName);
                }
                cfgNode = cfgNode.NextSibling;
            }
        }

        protected virtual void ParseNodeInput(NodeDef nodeDef, XmlElement elm)
        {
            Input input = new Input();
            input.name = elm.GetAttribute("name");
            input.desc = elm.GetAttribute("desc");
            if (elm.HasAttribute("connection_type"))
            {
                string connectType = elm.GetAttribute("connection_type");
                if (connectType.Length > 0)
                    input.connectType = connectType;
            }
            nodeDef.input.Add(input);
        }

        protected virtual void ParseNodeOutput(NodeDef nodeDef, XmlElement elm)
        {
            Output output = new Output();
            output.name = elm.GetAttribute("name");
            output.desc = elm.GetAttribute("desc");
            if (elm.HasAttribute("connection_type"))
            {
                string connectType = elm.GetAttribute("connection_type");
                if (connectType.Length > 0)
                    output.connectType = connectType;
            }
            nodeDef.output.Add(output);
        }

        protected virtual void ParseNodeParam(NodeDef nodeDef, XmlElement elm)
        {
            Param param = new Param();
            param.name = elm.GetAttribute("name");
            param.desc = elm.GetAttribute("desc");
            if (elm.HasAttribute("type"))
                param.type = elm.GetAttribute("type");

            nodeDef.param.Add(param);
        }

        protected virtual void ParseNodeEvent(NodeDef nodeDef,XmlElement elm)
        {
            EventInfo evtInfo = new EventInfo();
            evtInfo.name = elm.GetAttribute("name");
            evtInfo.desc = elm.GetAttribute("desc");

            if (elm.HasAttribute("params"))
                evtInfo.editorParams = bool.Parse(elm.GetAttribute("params"));

            nodeDef.events.Add(evtInfo);
        }

        protected virtual void ParseConnectDef(XmlElement elm)
        {
            ConnectionDef connectDef = new ConnectionDef();
            connectDef.name = elm.GetAttribute("name");
            connectDef.desc = elm.GetAttribute("desc");

            if (elm.HasAttribute("multi_input"))
                connectDef.multiInput = bool.Parse(elm.GetAttribute("multi_input"));

            if (elm.HasAttribute("multi_output"))
                connectDef.multiOutput = bool.Parse(elm.GetAttribute("multi_output"));

            if (elm.HasAttribute("color"))
            {
                string strColor = elm.GetAttribute("color");
                string[] rgb = strColor.Split(',');
                Vector3 vColor = new Vector3(float.Parse(rgb[0]), float.Parse(rgb[1]), float.Parse(rgb[2]));
                vColor /= 255.0f;
                connectDef.color = new Color(vColor.x, vColor.y, vColor.z);
            }
            editor.connectDef.Add(connectDef);
        }

        protected virtual void ParseEventDef(XmlElement elm)
        {
            GlobalEventDef evtDef = new GlobalEventDef();
            evtDef.name = elm.GetAttribute("name");
            evtDef.desc = elm.GetAttribute("desc");

            if (elm.HasAttribute("type"))
                evtDef.type = elm.GetAttribute("type");

            if (elm.HasAttribute("value"))
                evtDef.value = elm.GetAttribute("value");

            editor.globalEventDef.Add(evtDef);

            if (!elm.HasChildNodes)
            {
                return;
            }

            XmlNode cfgNode = elm.FirstChild;
            while (cfgNode != null)
            {
                XmlElement cfgElm = cfgNode as XmlElement;
                string cfgElmName = cfgElm.Name;

                if (cfgElmName.Equals("param"))
                {
                    ParseEventParam(evtDef, cfgElm);
                }
                else
                {
                    UnityEngine.Debug.LogError("can not parse element :" + cfgElmName);
                }

                cfgNode = cfgNode.NextSibling;
            }
        }

        protected virtual void ParseEventParam(GlobalEventDef evtDef, XmlElement elm)
        {
            Param param = new Param();
            param.name = elm.GetAttribute("name");
            param.desc = elm.GetAttribute("desc");

            if (elm.HasAttribute("type"))
                param.type = elm.GetAttribute("type");

            evtDef.paramList.Add(param);
        }
    }

    public class NodeFlowEditorLoader
    {
        static NodeFlowEditorSerializeBase serialize = null;
        public static void LoadEditor(NodeFlowEditorWindow editor, string path)
        {
            serialize = editor.serialize;
            serialize.LoadMeataEditor(path);
        }

        public static NodeFlow Load(string path)
        {
            return serialize.ParseFlow(path);
        }
    }

    public class NodeFlowEditorSaver
    {
        public static void SaveToXml(NodeFlowEditorWindow editor, string path, NodeFlow flow)
        {
            editor.serialize.SaveToXml(editor,path, flow);
        }
    }
}