using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;


//表现层的默认绘制helper ,相对稳定. 支持最有可能被定制扩展的部分： nodeDef 和 param 

namespace NodeFlow
{
    public class NodeFlowDrawHelper
    {
        static NodeFlow curFlow;
        public static void DrawFlow(NodeFlow flow)
        {
            curFlow = flow;
            if (flow == null) return;
            Dictionary<int, Node> nodes = flow.GetNodes();
            foreach (KeyValuePair<int, Node> kv in nodes)
            {
                DrawNode(kv.Value);
            }
            List<Connection> connections = flow.GetConnections();
            for (int i = 0; i < connections.Count; ++i)
            {
                DrawConnection(connections[i]);
            }
        }

        public static void DrawNode(Node node)
        {
            if (NodeFlowEditorWindow.instance.GetSelectedNodeId() == node.id)
            {
                GUI.color = Color.green;
            }
            //running display
            if (node.isRunning)
            {
                GUI.color = Color.blue;
            }
            node.rect = node.nodeDef.DrawNode(node);
            GUI.color = Color.white;
        }

        //default node renderer
        public static void NodeWindow(int id)
        {
            if (curFlow == null) return;
            Node node = curFlow.GetNodeByID(id);
            if (node == null) return;
            GUI.DragWindow(new Rect(0, 0, node.rect.width, 20));
            //in 
            {               
                for (int i = 0; i < node.preNodeList.Count; ++i)
                {
                    PreNode pre = node.preNodeList[i];
                    int height = 30 + pre.inIdx * 30;
                    Rect rect = new Rect(20, height, 60, 30);
                    GUI.Label(rect, pre.desc);
                }
            }

            //out
            {
              
                for (int i = 0; i < node.nextNodeList.Count; ++i)
                {
                    NextNode next = node.nextNodeList[i];
                    int height = 30 + next.outIdx * 30;
                    Rect rect = new Rect(node.rect.width - 60, height, 60, 30);
                    GUI.Label(rect, next.desc);
                }
            }

            //seocket cell
            {
                for (int i = 0; i < node.preNodeList.Count; ++i)
                {
                    PreNode pre = node.preNodeList[i];
                    Color c = pre.connectDef != null ? pre.connectDef.color : Color.white;
                    GUI.color = c;
                    GUI.Box(pre.rect, "");
                }

                for (int i = 0; i < node.nextNodeList.Count; ++i)
                {
                    NextNode next = node.nextNodeList[i];
                    Color c = next.connectDef != null ? next.connectDef.color : Color.white;
                    GUI.color = c;
                    GUI.Box(next.rect, "");
                }
                GUI.color = Color.white;
            }
            //delete button
            if (NodeFlowEditorWindow.instance.GetSelectedNodeId() == id)
            {
                if (GUI.Button(new Rect(node.rect.width / 2 - 10, node.rect.height - 20, 20, 20), "X"))
                {
                    //del node
                    OperCmdInfo cmdInfo = new OperCmdInfo();
                    cmdInfo.cmd = OperCmd.REMOVE_NODE;
                    cmdInfo.param = id;
                    NodeFlowEditorWindow.instance.AddCmd(cmdInfo);
                }
            }
        }

        public static void DrawConnection(Connection connection)
        {
            if (curFlow == null) return;
            Node outNode = curFlow.GetNodeByID(connection.outId);
            if (outNode == null) return;
            Node inNode = curFlow.GetNodeByID(connection.inId);
            if (inNode == null) return;
            NextNode next = outNode.nextNodeList[connection.outIdx];
            PreNode pre = inNode.preNodeList[connection.inIndex];
            Vector2 start = next.rect.center;
            start.x += outNode.rect.left;
            start.y += outNode.rect.top;
            Vector2 end = pre.rect.center;
            end.x += inNode.rect.left;
            end.y += inNode.rect.top;
            DrawLine(start, end, connection.color);
        }

        static public void DrawLine(Vector2 start, Vector2 end, Color color, float linewidth = 2.0f)
        {
            Vector3 startPos = new Vector3(start.x, start.y, 0);
            Vector3 endPos = new Vector3(end.x, end.y, 0);
            Vector3 startTan = startPos + Vector3.right * 50;
            Vector3 endTan = endPos + Vector3.left * 50;
            Handles.DrawBezier(startPos, endPos, startTan, endTan, color, null, linewidth);
        }

        static public Texture2D CreateTagTex(Color c0, Color c1, int width = 16, int heigh = 16)
        {
            Texture2D tex = new Texture2D(width, heigh);
            tex.name = "[Generated] Checker Texture";
            tex.hideFlags = HideFlags.DontSave;
            for (int y = 0; y < 8; ++y) for (int x = 0; x < 8; ++x) tex.SetPixel(x, y, c1);
            for (int y = 8; y < 16; ++y) for (int x = 0; x < 8; ++x) tex.SetPixel(x, y, c0);
            for (int y = 0; y < 8; ++y) for (int x = 8; x < 16; ++x) tex.SetPixel(x, y, c0);
            for (int y = 8; y < 16; ++y) for (int x = 8; x < 16; ++x) tex.SetPixel(x, y, c1);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }
    }

    public class NodePropertyLayoutHelper
    {
        static Node lastNode = null;
        public static void Show(Node node)
        {
            if (node == null)
            {
                lastNode = node;
                return;
            }

            if(lastNode != node)
            {
                GUI.FocusControl("-1");
                lastNode = node;
            }

            for (int i = 0; i < node.property.Count; ++i)
            {
                GUI.SetNextControlName(i.ToString());
                ParamProperty pro = node.property[i];
                pro.parmDef.Draw(pro);
            }

            for(int i = 0;i < node.eventProperty.Count;++i)
            {
                GUI.SetNextControlName(i.ToString());
                EventProperty pro = node.eventProperty[i];
                int lastIdx = pro.selectIdx;
                pro.selectIdx = EditorGUILayout.Popup(pro.desc, pro.selectIdx, NodeFlowEditorWindow.instance.globalEventList);
                if(pro.selectIdx != lastIdx)
                {
                    pro.param.Clear();
                    GlobalEventDef eventDef = NodeFlowEditorWindow.instance.globalEventDef[pro.selectIdx];
                    if (eventDef != null)
                    {
                        eventDef.Init(pro);
                    }
                }
            
                if (pro.eventDef != null)
                {
                    pro.eventDef.Draw(pro);
                }
            }
        }
    }

    public class NodeListLayoutHelper
    {
        private static bool m_bShowNoneNode = false;
        private static bool m_bShowLogicNode = false;
        private static bool m_bShowDisplayNode = false;

        public static void Layout(List<NodeDef> nodeDescs)
        {
            for (int i = 0; i < nodeDescs.Count; ++i)
            {
                NodeDef desc = nodeDescs[i];
                desc.DrawInNodeList();
            }
        }
    }


    public class DirInfo
    {
        public List<DirInfo> dirs = new List<DirInfo>();
        public List<string> files = new List<string>();
        public string path = "";
        public bool isFoldout = false;

        public void Reload()
        {
            dirs.Clear();
            files.Clear();

            DirectoryInfo dirInfo = new DirectoryInfo(path);
            DirectoryInfo[] subDirInfo = dirInfo.GetDirectories();
            if (subDirInfo != null)
            {
                foreach (DirectoryInfo sub in subDirInfo)
                {
                    if (sub.Name == "." || sub.Name == ".." || sub.Name == ".svn")
                        continue;
                    DirInfo dir = new DirInfo();
                    dir.path = sub.FullName;

                    dirs.Add(dir);
                }
            }
            FileInfo[] subFileInfo = dirInfo.GetFiles("*.xml");
            if (subDirInfo != null)
            {
                foreach (FileInfo sub in subFileInfo)
                {
                    files.Add(sub.FullName);
                }
            }
            for (int i = 0; i < dirs.Count; ++i)
            {
                dirs[i].Reload();
            }
        }

        public void Layout()
        {
            //EditorGUILayout.Space();
            EditorGUILayout.Separator();
            string str = path.Substring(path.LastIndexOf("\\") + 1);
            isFoldout = EditorGUILayout.Foldout(isFoldout, str);
            if (isFoldout)
            {
                GUILayout.BeginVertical();
                for (int i = 0; i < dirs.Count; ++i)
                {
                    dirs[i].Layout();
                }
                for (int i = 0; i < files.Count; ++i)
                {
                    string fileName = files[i];
                    str = fileName.Substring(fileName.LastIndexOf("\\") + 1);
                    if (GUILayout.Button(str, GUILayout.Width(200), GUILayout.Height(25)))
                    {
                        OperCmdInfo cmdInfo = new OperCmdInfo();
                        cmdInfo.cmd = OperCmd.OPEN_FILE;
                        cmdInfo.param = files[i];
                        NodeFlowEditorWindow.instance.AddCmd(cmdInfo);
                    }
                }
                GUILayout.EndVertical();
            }
        }
    }

    public class NodeFlowFileList
    {
        public string rootPath = "Assets\\Resources\\";
        public DirInfo dir = new DirInfo();

        public void Reload(string editorName)
        {
            dir.path = rootPath + "NodeFlow\\"+ editorName+"\\";
            dir.Reload();
            dir.isFoldout = true;
        }

        public string GetDirRoot(string editorName)
        {
            return rootPath + "NodeFlow\\" + editorName + "\\";
        }

        public void Layout()
        {
            dir.Layout();
        }
    }

    public class NodeFlowEditorModel
    {

        public NodeFlow mCurrent = null;
        NodeFlow mLastNodeFlow = null;
        public bool IsNewNodeFlow()
        {
            if(mCurrent != mLastNodeFlow)
            {
                mLastNodeFlow = mCurrent;
                return true;
            }
            return false;
        }

        public void LoadFlowByPath(string path)
        {
            Release();

            mCurrent = NodeFlowEditorLoader.Load(path);
            if(mCurrent != null)
            {
                mCurrent.name = path;
            }
        }

        public void CreateNewFlow()
        {
            Release();
            mCurrent = new NodeFlow();
            mCurrent.name = "";
        }

        public void ReLoadFlow()
        {
            Release();
            if(mCurrent != null && mCurrent.name.Length > 0)
                mCurrent = NodeFlowEditorLoader.Load(mCurrent.name);
        }

        public void Release()
        {
            if (mCurrent != null)
            {
                mCurrent.Release();
                mCurrent = null;
            }
        }

        public void Draw()
        {
            NodeFlowDrawHelper.DrawFlow(mCurrent);
        }

        public void ShowProperty(int nodeId)
        {
            if (mCurrent == null) return;
            Node node = mCurrent.GetNodeByID(nodeId);
            NodePropertyLayoutHelper.Show(node);
        }

        public void PasteProperty(int nodeId, string key, string value)
        {
            if (mCurrent == null) return;
            int idx = int.Parse(key);
            Node node = mCurrent.GetNodeByID(nodeId);
            for (int i = 0; i < node.property.Count; ++i)
            {
                ParamProperty pro = node.property[i];

                if (i == idx)
                {
                    pro.strValue = value;
                    return;
                }
            }
        }

        public string CopyProperty(int nodeId, string key)
        {
            if (mCurrent == null) return "";
            int idx = int.Parse(key);
            Node node = mCurrent.GetNodeByID(nodeId);
            if (node == null)
                return "";
            for (int i = 0; i < node.property.Count; ++i)
            {
                ParamProperty pro = node.property[i];

                if (i == idx)
                {
                    return pro.strValue;
                }
            }
            return "";
        }

        public void OnUpdate()
        {
            
        }
    }
}