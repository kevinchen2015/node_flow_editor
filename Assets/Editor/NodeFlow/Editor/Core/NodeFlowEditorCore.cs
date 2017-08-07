using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System;
using System.Reflection;


//编辑器核心逻辑对象,稳定

namespace NodeFlow
{
    public class NodeFlow
    {
        public string name = "";
        Dictionary<int, Node> nodes = new Dictionary<int, Node>();
        List<Connection> connections = new List<Connection>();
        int nodeIdx = 0;

        public Dictionary<int, Node> GetNodes()
        {
            return nodes;
        }

        public List<Connection> GetConnections()
        {
            return connections;
        }

        public void AddNode(Node node)
        {
            nodes.Add(node.id, node);

            if (node.id > nodeIdx)
                nodeIdx = node.id;
        }

        public int GenNodeId()
        {
            return ++nodeIdx;
        }
    
        public void AddConnection(Connection connection)
        {
            connections.Add(connection);
        }

        public Node GetNodeByID(int id)
        {
            Node node = null;
            if(nodes.TryGetValue(id,out node))
            {
                return node;
            }
            return null;
        }

        public void Parse()
        {
           
        }

        public void Release()
        {
            foreach (KeyValuePair<int, Node> kv in nodes)
            {
                kv.Value.Release();
            }
            nodes.Clear();
            connections.Clear();
            nodeIdx = 0;
        }

        public void UnBindConnection(NextNode next, PreNode prev,Connection connect)
        {
            connections.Remove(connect);
            prev.RemovePort(next.parent.id, next.outIdx);
            next.RemovePort(prev.parent.id, prev.inIdx);
        }

        public void BindConnection(NextNode next,PreNode prev,ConnectionDef def)
        {
            if (prev == null || next == null) return;
            for (int i=0;i < connections.Count;++i)
            {
                Connection connection = connections[i];

                if( connection.outId == next.parent.id 
                    && connection.inId == prev.parent.id
                    && connection.outIdx == next.outIdx
                    && connection.inIndex == prev.inIdx
                    )
                {
                    UnBindConnection(next, prev, connection);
                    return; 
                }
            }

            if(prev.connectDef != next.connectDef)
            {
                UnityEngine.Debug.LogError("接口类型不同!");
                return;
            }

            if(prev.connectDef.multiInput == false)
            {
                if(prev.GetPortCount() >= 1)
                {
                    UnityEngine.Debug.LogError("入口连线数不能超过1");
                    return;
                }
            }

            if (next.connectDef.multiInput == false)
            {
                if (next.GetPortCount() >= 1)
                {
                    UnityEngine.Debug.LogError("出口连线数不能超过1");
                    return;
                }
            }

            Connection newConnect = new Connection();
            newConnect.connectDef = def;
            newConnect.connectDef.Init(newConnect);
            newConnect.outId = next.parent.id;
            newConnect.outIdx = next.outIdx;
            newConnect.outName = next.name;
            newConnect.inId = prev.parent.id;
            newConnect.inIndex = prev.inIdx;
            newConnect.inName = prev.name;
            AddConnection(newConnect);

            prev.AddPort(next.parent.id, next.outIdx);
            next.AddPort(prev.parent.id, prev.inIdx);
        }


        public void SetNodeActive(int id,bool active)
        {
            Node node = GetNodeByID(id);
            if (node == null) return;
            node.isRunning = active;
        }

        public void RemoveNode(int id)
        {
            Node node = GetNodeByID(id);
            if (node == null) return;

            List<Connection> needRemoveConnect = new List<Connection>();
            for (int i = 0; i < connections.Count; ++i)
            {
                if (connections[i].inId == id)
                {
                    Node next = GetNodeByID(connections[i].outId);
                    if(next != null)
                    {
                        next.nextNodeList[connections[i].outIdx].RemoveByNodeId(id);
                    }
                    needRemoveConnect.Add(connections[i]);
                }
                
                if(connections[i].outId == id)
                {
                    Node prev = GetNodeByID(connections[i].inId);
                    if (prev != null)
                    {
                        prev.preNodeList[connections[i].inIndex].RemoveByNodeId(id);
                    }
                    needRemoveConnect.Add(connections[i]);
                }
            }

            nodes.Remove(node.id);
            node.Release();
            foreach (Connection c in needRemoveConnect)
            {
                connections.Remove(c);
            }
        }

        public Node CreateNode(NodeDef nodeDef,Vector2 pos,int id = -1)
        {
            Node node = new Node(this);
            node.id = id > 0?id:this.GenNodeId();
            node.nodeDef = nodeDef;
            node.rect.x = pos.x;
            node.rect.y = pos.y;
            node.Parse();
            this.AddNode(node);
            return node;
        }

        public void CloneNode(Node srcNode)
        {
            Node node = new Node(this);
            node.id = this.GenNodeId();
            node.nodeDef = srcNode.nodeDef;
            node.rect = srcNode.rect;
            node.rect.x += 50;
            node.rect.y += 50;
            node.Parse();
            node.Clone(srcNode);
            this.AddNode(node);
        }
    }

    public class ParamProperty
    {
        public string name;
        public string desc;
        public string type;
        public string strValue = "";
        public Param parmDef;

        public void Clone(ParamProperty src)
        {
            this.name = src.name;
            this.desc = src.desc;
            this.type = src.type;
            this.strValue = src.strValue;
            this.parmDef = src.parmDef;
        }
    }


    public class EventProperty
    {
        public string name;
        public string desc;
        public string strValue = "";
        public bool editorParams = false;
        public GlobalEventDef eventDef = null;
        public List<ParamProperty> param = new List<ParamProperty>();
        public bool isFoldout = true;
        public int selectIdx = -1;

        public void Clone(EventProperty src)
        {
            this.name = src.name;
            this.desc = src.desc;
            this.strValue = src.strValue;
            this.editorParams = src.editorParams;
            this.eventDef = src.eventDef;
            this.selectIdx = src.selectIdx;

            param.Clear();
            foreach (ParamProperty srcP in src.param)
            {
                ParamProperty thisP = new ParamProperty();
                thisP.Clone(srcP);
                param.Add(thisP);
            }
        }
    }
   

    public struct PortInfo
    {
        public int nodeId;
        public int portIdx;

        public PortInfo(int nodeId,int portIdx)
        {
            this.nodeId = nodeId;
            this.portIdx = portIdx;
        }
    }

    public class PortBase
    {
        public ConnectionDef connectDef;
        List<PortInfo> portList = new List<PortInfo>();

        public int GetPortCount()
        {
            return portList.Count;
        }

        public virtual void Clear()
        {
            portList.Clear();
        }

        public void AddPort(int nodeId, int portIdx)
        {
            RemovePort(nodeId, portIdx);
            portList.Add(new PortInfo(nodeId,portIdx));
        }

        public void RemovePort(int nodeId,int portIdx)
        {
            for(int i = 0;i < portList.Count;++i)
            {
                PortInfo info = portList[i];
                if (info.nodeId == nodeId && info.portIdx == portIdx)
                {
                    portList.RemoveAt(i);
                    return;
                }
            }
        }

        public void RemoveByNodeId(int nodeId)
        {
            for (int i = 0; i < portList.Count; )
            {
                PortInfo info = portList[i];
                if (info.nodeId == nodeId)
                {
                    portList.RemoveAt(i);
                }
                else
                {
                    ++i;
                }
            }
        }
    }

    public class PreNode : PortBase
    {
        public int inIdx;
        public string name;
        public string desc;
        public string connectType;
        public Rect rect;
        public Node parent;

        public PreNode(Node parent)
        {
            this.parent = parent;
        }
    }

    public class NextNode : PortBase
    {
        public int outIdx;
        public string name;
        public string desc;
        public string connectType;
        public Rect rect;
        public Node parent;
        public NextNode(Node parent)
        {
            this.parent = parent;
        }
    }

    public class Node
    {
        public NodeFlow parent = null;
        public NodeDef nodeDef = null;
        public int id;
        public string name;
        public string desc;
        public string type;
        public bool isRunning = false;
        public Color color = Color.black;
        public List<PreNode> preNodeList = new List<PreNode>();
        public List<NextNode> nextNodeList = new List<NextNode>();
        public List<ParamProperty> property = new List<ParamProperty>();
        public List<EventProperty> eventProperty = new List<EventProperty>();
        public Rect rect;

        public Node(NodeFlow parent)
        {
            this.parent = parent;
        }

        public EventProperty GetEventProperty(string name)
        {
            for (int i = 0; i < eventProperty.Count; ++i)
            {
                if (eventProperty[i].name == name)
                {
                    return eventProperty[i];
                }
            }
            return null;
        }

        public int GetPreNodeIdxByName(string name)
        {
            for (int i = 0; i < preNodeList.Count; ++i)
            {
                if (preNodeList[i].name == name)
                {
                    return preNodeList[i].inIdx;
                }
            }
            return -1;
        }

        public string GetPreNodeNameByIdx(int idx)
        {
            for(int i = 0;i < preNodeList.Count;++i)
            {
                if(preNodeList[i].inIdx == idx)
                {
                    return preNodeList[i].name;
                }
            }
            return "";
        }

        public int GetNextNodeIdxByName(string name)
        {
            for (int i = 0; i < nextNodeList.Count; ++i)
            {
                if (nextNodeList[i].name == name)
                {
                    return nextNodeList[i].outIdx;
                }
            }
            return -1;
        }

        public string GetNextNodeNameByIdx(int idx)
        {
            for (int i = 0; i < nextNodeList.Count; ++i)
            {
                if (nextNodeList[i].outIdx == idx)
                {
                    return nextNodeList[i].name;
                }
            }
            return "";
        }

        public void Release()
        {
            preNodeList.Clear();
            nextNodeList.Clear();
            property.Clear();
            eventProperty.Clear();
        }

        public void Clone(Node srcNode)
        {
            for (int i = 0;i < property.Count;++i)
            {
                property[i].Clone(srcNode.property[i]);
            }

            for(int i = 0;i < eventProperty.Count;++i)
            {
                eventProperty[i].Clone(srcNode.eventProperty[i]);
            }
        }

        public void Parse()
        {
            Release();
            nodeDef.Init(this);
            ResetSize();
        }

        public void ResetSize()
        {
            //sise
            rect.width = 160;
            int maxNum = preNodeList.Count > nextNodeList.Count ? preNodeList.Count : nextNodeList.Count;
            rect.height = 40 + maxNum * 30;

            float socketCellSize = 20.0f;
            //in pos
            for (int i = 0; i < preNodeList.Count; ++i)
            {
                PreNode preNode = preNodeList[i];
                preNode.rect = new Rect(0, 30 + preNode.inIdx * 30, socketCellSize, socketCellSize);
            }

            //out pos
            for (int i = 0;i < nextNodeList.Count;++i)
            {
                NextNode nextNode = nextNodeList[i];
                nextNode.rect = new Rect(rect.width - socketCellSize, 30 + nextNode.outIdx * 30, socketCellSize, socketCellSize);
            }
        }

    }

    public class Connection
    {
        public string name;
        public int outId;
        public int outIdx; 
        public int inId;
        public int inIndex;
        public string inName = "";
        public string outName = "";
        public bool multiInput = true;
        public bool multiOutput = true;
        public Color color = Color.white;
        public ConnectionDef connectDef = null;
    }

}
