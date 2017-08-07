using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System;
using System.Reflection;


//编辑器窗口 ,包括ui布局和输入,相对稳定

namespace NodeFlow
{
    public enum OperCmd
    {
        REMOVE_NODE = 0,
        //REMOVE_CONNECTION,
        CREATE_NODE,
        CLONE_NODE,
        OPEN_FILE,
        SAVE_FILE,
        CREATE_NEW,
        NODE_ACTIVE,   //for running debug mode
    }

    public struct OperCmdInfo
    {
        public OperCmd cmd;
        public object param;
        public object param2;
    }

    public class NodeFlowEditorWindow : EditorWindow
    {
        static public NodeFlowEditorWindow instance;
  
        int windowWidth = 1440;
        int windowHeight = 800;
        float commandHeight = 30;
        float leftWidth = 200;
        float rightWidth = 300;
        Vector2 nodeFlowViewPos;
        int selectedNodeId = -1;
        Vector2 fileListViewPos;
        Vector2 propertyViewPos;
        public string edtorName = "NodeFlow";
        public string version = "0.0";

        public NextNode curNextNode = null;
        Vector2 curMouseEndPos;
        NodeFlowEditorModel editorMode = new NodeFlowEditorModel();
        public NodeFlowEditorSerializeBase serialize = null;
        public List<NodeDef> nodeDef = new List<NodeDef>();
        public List<ConnectionDef> connectDef = new List<ConnectionDef>();
        public List<GlobalEventDef> globalEventDef = new List<GlobalEventDef>();
        public string[] globalEventList = null;
        List<OperCmdInfo> operList = new List<OperCmdInfo>();
        NodeFlowFileList filesList = new NodeFlowFileList();
        TextEditor textEditor = new TextEditor();
        bool isFileListMode = true;

        public int GetSelectedNodeId()
        {
            return selectedNodeId;
        }

        void OnEnable()
        {
            instance = this;
            this.minSize = new Vector2(windowWidth, windowHeight);
        }

        public void OnShow()
        {
            this.minSize = new Vector2(windowWidth, windowHeight);
            filesList.Reload(this.edtorName);
            OperCmdInfo cmdInfo = new OperCmdInfo();
            cmdInfo.cmd = OperCmd.CREATE_NEW;
            NodeFlowEditorWindow.instance.AddCmd(cmdInfo);
        }

        void OnDisable()
        {
            editorMode.Release();
        }

        public void OnEditorMetaLoaded()
        {
            globalEventList = new string[globalEventDef.Count];
            for (int i = 0;i < globalEventDef.Count;++i)
            {
                globalEventList[i] = globalEventDef[i].desc;
            }
        }

        public NodeDef GetNodeDef(string name)
        {
            for (int i = 0; i < nodeDef.Count; ++i)
            {
                if (nodeDef[i].name.Equals(name))
                {
                    return nodeDef[i];
                }
            }
            return null;
        }

        public ConnectionDef GetConnectionDef(string name)
        {
            for (int i = 0; i < connectDef.Count; ++i)
            {
                if (connectDef[i].name.Equals(name))
                {
                    return connectDef[i];
                }
            }
            return new ConnectionDef();
        }

        public GlobalEventDef GetGlobalEventDef(string name)
        {
            for (int i = 0; i < globalEventDef.Count; ++i)
            {
                if (globalEventDef[i].name.Equals(name))
                {
                    return globalEventDef[i];
                }
            }
            return null;
        }

        public void AddCmd(OperCmdInfo info)
        {
            operList.Add(info);
        }

        Vector2 MousePosToViewPos(Vector2 mousePos)
        {
            Vector2 pos = mousePos + nodeFlowViewPos;
            pos.x -= leftWidth;
            pos.y -= commandHeight;
            return pos;
        }

        bool IsMousePosInNodeEditorView(Vector2 mousePos)
        {
            float skillFlowWidth = position.width - leftWidth - rightWidth;
            Rect rect = new Rect(leftWidth, commandHeight, skillFlowWidth, position.height - commandHeight);

            if (rect.Contains(mousePos))
                return true;

            return false;
        }

        void OnDoEnevent()
        {
            Event currentEvent = Event.current;
            if (currentEvent.button == 0)
            {
                Vector2 vMousePos = Event.current.mousePosition;

                if (currentEvent.type == EventType.MouseDown)
                {
                    //只做编辑区域内的点击判断
                    if (IsMousePosInNodeEditorView(vMousePos))
                    {
                        if(curNextNode == null)
                        {
                            curNextNode = GetNextNodeSocket(vMousePos);

                            if (curNextNode != null)
                            {
                                curMouseEndPos = MousePosToViewPos(vMousePos);
                                Repaint();
                            }
                        }
                        else
                        {
                            NextNode nextNode = GetNextNodeSocket(vMousePos);

                            if(nextNode != curNextNode)
                            {
                                PreNode preNode = GetNodeInSocket(vMousePos);
                                //bind connection
                                BindConnection(curNextNode, preNode, curNextNode.connectDef);
                                curNextNode = null;
                                Repaint();
                            }
                        }
                    }
                }
                else if (currentEvent.type == EventType.MouseDrag)
                {
                    if (curNextNode != null)
                    {
                        curMouseEndPos = MousePosToViewPos(vMousePos);
                        Repaint();
                    }
                }
                else if (currentEvent.type == EventType.MouseUp)
                {
                    if (IsMousePosInNodeEditorView(vMousePos))
                    {
                        if (curNextNode != null)
                        {
                            PreNode preNode = GetNodeInSocket(vMousePos);
                            if(preNode!= null)
                            {
                                //bind connection
                                BindConnection(curNextNode, preNode, curNextNode.connectDef);
                                curNextNode = null;
                            }
                        }
                        else
                        {
                            selectedNodeId = GetSelectNodeId(vMousePos);
                        }
                        Repaint();
                    }

                }
            }

            if (EventType.ContextClick == currentEvent.type)
            {
                Vector2 vMousePos = currentEvent.mousePosition;
                selectedNodeId = GetSelectNodeId(vMousePos);
                curNextNode = null;

                if (selectedNodeId > -1)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clone"), false, CloneCmd, selectedNodeId);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Delete"), false, DeleteCmd, selectedNodeId);
                    menu.ShowAsContext();
                    currentEvent.Use();
                }
                Repaint();
            }

            if (selectedNodeId > -1)
            {
                if (currentEvent.isKey)
                {
                    Repaint();
                    string name = GUI.GetNameOfFocusedControl();
                    int idx = 0;
                    if (!int.TryParse(name, out idx)) return;
                    if (currentEvent.keyCode == KeyCode.C && currentEvent.control)
                    {
                        string value = editorMode.CopyProperty(selectedNodeId, name);

                        textEditor.text = value;
                        textEditor.SelectAll();
                        textEditor.Copy();
                    }
                    if (currentEvent.keyCode == KeyCode.V && currentEvent.control)
                    {
                        textEditor.text = "";
                        textEditor.Paste();
                        string value = textEditor.text;
                        editorMode.PasteProperty(selectedNodeId, name, value);
                    }
                    Repaint();
                }
            }
        }

        void CloneCmd(object obj)
        {
            int id = (int)obj;
            if (editorMode.mCurrent == null) return;
            Node node = editorMode.mCurrent.GetNodeByID(id);
            if (node == null) return;
            OperCmdInfo cmdInfo = new OperCmdInfo();
            cmdInfo.cmd = OperCmd.CLONE_NODE;
            cmdInfo.param = node;
            AddCmd(cmdInfo);
        }

        void DeleteCmd(object obj)
        {
            int id = (int)obj;
            OperCmdInfo cmdInfo = new OperCmdInfo();
            cmdInfo.cmd = OperCmd.REMOVE_NODE;
            cmdInfo.param = id;
            AddCmd(cmdInfo);
        }

        void BindConnection(NextNode next, PreNode prev, ConnectionDef def )
        {
            if (editorMode.mCurrent == null) return;
            editorMode.mCurrent.BindConnection(next, prev ,def);
        }

        NextNode GetNextNodeSocket(Vector2 mousePt)
        {
            if (editorMode.mCurrent == null) return null;
            Vector2 pos = MousePosToViewPos(mousePt);
            Dictionary<int, Node> nodes = editorMode.mCurrent.GetNodes();
            foreach (KeyValuePair<int, Node> kv in nodes)
            {
                Node node = kv.Value;

                Vector2 p = pos;
                p.x -= node.rect.left;
                p.y -= node.rect.top;

                for (int j = 0; j < node.nextNodeList.Count; ++j)
                {
                    if (node.nextNodeList[j].rect.Contains(p))
                    {
                        return node.nextNodeList[j];
                    }
                }
            }
            return null;
        }

        PreNode GetNodeInSocket(Vector2 mousePt)
        {
            if (editorMode.mCurrent == null) return null;
            Vector2 pos = MousePosToViewPos(mousePt);
            Dictionary<int, Node> nodes = editorMode.mCurrent.GetNodes();
            foreach (KeyValuePair<int, Node> kv in nodes)
            {
                Node node = kv.Value;
                Vector2 p = pos;
                p.x -= node.rect.left;
                p.y -= node.rect.top;

                foreach(PreNode preNode in node.preNodeList)
                {
                    if (preNode.rect.Contains(p))
                    {
                        return preNode;
                    }
                }
            }
            return null;
        }

        int GetSelectNodeId(Vector2 mousePt)
        {
            if (editorMode.mCurrent == null) return -1;
            Vector2 pos = MousePosToViewPos(mousePt);
            Dictionary<int, Node> nodes = editorMode.mCurrent.GetNodes();
            foreach (KeyValuePair<int, Node> kv in nodes)
            {
                Node node = kv.Value;
                if (node.rect.Contains(pos))
                {
                    return node.id;
                }
            }
            return -1;
        }

        public void Update()
        {
            for (int i = 0; i < operList.Count; ++i)
            {
                OperCmdInfo cmdInfo = operList[i];
                switch (cmdInfo.cmd)
                {
                    case OperCmd.NODE_ACTIVE:
                        {
                            if(editorMode.mCurrent != null)
                            {
                                editorMode.mCurrent.SetNodeActive((int)cmdInfo.param,(bool)cmdInfo.param2);
                            }
                        }
                        break;

                    case OperCmd.REMOVE_NODE:
                        {
                            if (editorMode.mCurrent != null)
                            {
                                editorMode.mCurrent.RemoveNode((int)cmdInfo.param);
                            }
                        }
                        break;

                    case OperCmd.CREATE_NODE:
                        {
                            if (editorMode.mCurrent != null)
                            {
                                Vector2 pos = new Vector2(200, 100);
                                editorMode.mCurrent.CreateNode(cmdInfo.param as NodeDef, pos);
                            }
                        }
                        break;

                    case OperCmd.CLONE_NODE:
                        {
                            if (editorMode.mCurrent != null)
                            {
                                editorMode.mCurrent.CloneNode(cmdInfo.param as Node);
                            }
                        }
                        break;

                    case OperCmd.OPEN_FILE:
                        {
                            string name = (string)cmdInfo.param;
                            selectedNodeId = -1;
                            editorMode.LoadFlowByPath(name);

                            Repaint();
                        }
                        break;

                    case OperCmd.CREATE_NEW:
                        {
                            selectedNodeId = -1;
                            editorMode.CreateNewFlow();
                        }
                        break;

                    case OperCmd.SAVE_FILE:
                        {
                            if (editorMode.mCurrent != null)
                            {
                                bool needReLoad = false;
                                if (editorMode.mCurrent.name.Length == 0)
                                {
                                    //to show save dalog
                                    //needReLoad = true;
                                    UnityEngine.Debug.LogError("文件名为空");
                                }
                                else
                                {
                                    NodeFlowEditorSaver.SaveToXml(this, editorMode.mCurrent.name, editorMode.mCurrent);
                                    needReLoad = true;
                                }

                                if (needReLoad)
                                {
                                    if (Application.isPlaying == true)
                                    {
                                        editorMode.ReLoadFlow();
                                        Repaint();
                                    }
                                    filesList.Reload(this.edtorName);
                                }
                            }
                        }
                        break;
                }
            }
            operList.Clear();
            if (Application.isPlaying)
            {
                editorMode.OnUpdate();
            }
        }

        void OnGUI()
        {
            //command button
            {
                Rect rect = new Rect(0, 0, position.width, commandHeight);
                GUILayout.BeginArea(rect);
                GUI.Box(new Rect(0, 0, rect.width, rect.height), "");
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("文件列表", GUILayout.Width(95), GUILayout.Height(30)))
                {
                    isFileListMode = true;
                }
                if (GUILayout.Button("节点列表", GUILayout.Width(95), GUILayout.Height(30)))
                {
                    isFileListMode = false;
                }
                GUILayout.Label("  ", GUILayout.Width(30), GUILayout.Height(30));
                if (GUILayout.Button("创建空NodeFlow", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    OperCmdInfo cmdInfo = new OperCmdInfo();
                    cmdInfo.cmd = OperCmd.CREATE_NEW;
                    NodeFlowEditorWindow.instance.AddCmd(cmdInfo); 
                }
                if (GUILayout.Button("保存NodeFlow", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    OperCmdInfo cmdInfo = new OperCmdInfo();
                    cmdInfo.cmd = OperCmd.SAVE_FILE;
                    NodeFlowEditorWindow.instance.AddCmd(cmdInfo);
                }
                if (editorMode.IsNewNodeFlow())
                {
                    GUI.FocusControl("-1");
                }
                if (editorMode.mCurrent != null)
                {
                    string flowName = "文件名:";
                    editorMode.mCurrent.name = EditorGUILayout.TextField(flowName, editorMode.mCurrent.name);

                    if (GUILayout.Button("打开所在文件夹", GUILayout.Width(150), GUILayout.Height(30)))
                    {
                        string path = editorMode.mCurrent.name;

                        if(path.Length == 0)
                        {
                            path = filesList.GetDirRoot(edtorName);
                        }
                        else
                        {
                            path = path.Replace("/", "\\");
                            int idx = path.LastIndexOf("\\");
                            path = path.Substring(0, idx);
                        }
                        System.Diagnostics.Process.Start("explorer", path);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            //file list view
            {
                Rect rect = new Rect(0, commandHeight, leftWidth, position.height - commandHeight);
                GUILayout.BeginArea(rect);
                GUI.Box(new Rect(0, 0, rect.width, rect.height), "");
                GUILayout.BeginVertical();
                fileListViewPos = EditorGUILayout.BeginScrollView(fileListViewPos, GUILayout.Width(rect.width), GUILayout.Height(rect.height));
                if (isFileListMode)
                {
                    filesList.Layout();
                }
                else
                {
                    NodeListLayoutHelper.Layout(nodeDef);
                }
                EditorGUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            //property view
            {
                Rect rect = new Rect(position.width - rightWidth, commandHeight, rightWidth, position.height - commandHeight);
                GUILayout.BeginArea(rect);
                GUI.Box(new Rect(0, 0, rect.width, rect.height), "");
                GUILayout.BeginVertical();
                propertyViewPos = EditorGUILayout.BeginScrollView(propertyViewPos, GUILayout.Width(rect.width), GUILayout.Height(rect.height));
                editorMode.ShowProperty(selectedNodeId);
                EditorGUILayout.EndScrollView();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            //nodeflow view 
            {
                float skillFlowWidth = position.width - leftWidth - rightWidth;
                Rect rect = new Rect(leftWidth, commandHeight, skillFlowWidth, position.height - commandHeight);
                GUILayout.BeginArea(rect);
                GUI.Box(new Rect(0, 0, rect.width, rect.height), "");
                GUILayout.BeginHorizontal();
                nodeFlowViewPos = EditorGUILayout.BeginScrollView(nodeFlowViewPos, GUILayout.Width(rect.width), GUILayout.Height(rect.height));
                GUILayout.Label("", GUILayout.Width(rect.width * 3), GUILayout.Height(rect.height * 3));
                BeginWindows();
                editorMode.Draw();
                //link 
                if (curNextNode != null)
                {
                    Vector2 start = curNextNode.rect.center;
                    start.x += curNextNode.parent.rect.left;
                    start.y += curNextNode.parent.rect.top;
                    Vector2 end = curMouseEndPos;
                    ConnectionDef def = curNextNode.connectDef;
                    Color c = def != null ? def.color : Color.white;
                    NodeFlowDrawHelper.DrawLine(start, end, c);
                }
                EndWindows();
                EditorGUILayout.EndScrollView();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            OnDoEnevent();
        }
    }
}