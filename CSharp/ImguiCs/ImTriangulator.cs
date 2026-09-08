// Port of ImTriangulator (imgui_draw.cpp): triangulate concave polygons by ear clipping, O(N^2).
// Reference: https://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
using System;
using System.Collections.Generic;

namespace ImGui;

public enum ImTriangulatorNodeType
{
    ImTriangulatorNodeType_Convex,
    ImTriangulatorNodeType_Ear,
    ImTriangulatorNodeType_Reflex
}

public class ImTriangulatorNode
{
    public ImTriangulatorNodeType Type;
    public int Index;
    public ImVec2 Pos;
    public ImTriangulatorNode Next;
    public ImTriangulatorNode Prev;

    public void Unlink() { Next.Prev = Prev; Prev.Next = Next; }
}

public class ImTriangulator
{
    public int _TrianglesLeft = 0;
    ImTriangulatorNode[] _Nodes;
    List<ImTriangulatorNode> _Ears = new List<ImTriangulatorNode>();
    List<ImTriangulatorNode> _Reflexes = new List<ImTriangulatorNode>();

    public static int EstimateTriangleCount(int points_count) => (points_count < 3) ? 0 : points_count - 2;
    public static int EstimateScratchBufferSize(int points_count) => points_count * 3; // not used by C# port

    public ImTriangulator(ImVec2[] points, int points_count)
    {
        if (points_count >= 3)
            Init(points, points_count);
    }

    public int GetIdxCount() => _TrianglesLeft * 3;

    public void Init(ImVec2[] points, int points_count)
    {
        _TrianglesLeft = EstimateTriangleCount(points_count);
        _Nodes = new ImTriangulatorNode[points_count];
        for (int i = 0; i < points_count; i++) _Nodes[i] = new ImTriangulatorNode();
        _Ears.Clear();
        _Reflexes.Clear();
        BuildNodes(points, points_count);
        BuildReflexes();
        BuildEars();
    }

    void BuildNodes(ImVec2[] points, int points_count)
    {
        for (int i = 0; i < points_count; i++)
        {
            _Nodes[i].Type = ImTriangulatorNodeType.ImTriangulatorNodeType_Convex;
            _Nodes[i].Index = i;
            _Nodes[i].Pos = points[i];
            _Nodes[i].Next = _Nodes[(i + 1) % points_count];
            _Nodes[i].Prev = _Nodes[(i - 1 + points_count) % points_count];
        }
    }

    void BuildReflexes()
    {
        ImTriangulatorNode n1 = _Nodes[0];
        for (int i = _TrianglesLeft; i >= 0; i--, n1 = n1.Next)
        {
            if (ImGuiCore.ImTriangleIsClockwise(n1.Prev.Pos, n1.Pos, n1.Next.Pos))
                continue;
            n1.Type = ImTriangulatorNodeType.ImTriangulatorNodeType_Reflex;
            _Reflexes.Add(n1);
        }
    }

    void BuildEars()
    {
        ImTriangulatorNode n1 = _Nodes[0];
        for (int i = _TrianglesLeft; i >= 0; i--, n1 = n1.Next)
        {
            if (n1.Type != ImTriangulatorNodeType.ImTriangulatorNodeType_Convex)
                continue;
            if (!IsEar(n1.Prev.Index, n1.Index, n1.Next.Index, n1.Prev.Pos, n1.Pos, n1.Next.Pos))
                continue;
            n1.Type = ImTriangulatorNodeType.ImTriangulatorNodeType_Ear;
            _Ears.Add(n1);
        }
    }

    public void GetNextTriangle(uint[] out_triangle)
    {
        if (_Ears.Count == 0)
        {
            FlipNodeList();

            ImTriangulatorNode node = _Nodes[0];
            for (int i = _TrianglesLeft; i >= 0; i--, node = node.Next)
                node.Type = ImTriangulatorNodeType.ImTriangulatorNodeType_Convex;
            _Reflexes.Clear();
            BuildReflexes();
            BuildEars();

            if (_Ears.Count == 0)
            {
                System.Diagnostics.Debug.Assert(_TrianglesLeft > 0); // Geometry is degenerated
                _Ears.Add(_Nodes[0]);
            }
        }

        ImTriangulatorNode ear = _Ears[_Ears.Count - 1];
        _Ears.RemoveAt(_Ears.Count - 1);
        out_triangle[0] = (uint)ear.Prev.Index;
        out_triangle[1] = (uint)ear.Index;
        out_triangle[2] = (uint)ear.Next.Index;

        ear.Unlink();
        if (ear == _Nodes[0])
            _Nodes[0] = ear.Next;

        ReclassifyNode(ear.Prev);
        ReclassifyNode(ear.Next);
        _TrianglesLeft--;
    }

    void FlipNodeList()
    {
        ImTriangulatorNode first = _Nodes[0];
        ImTriangulatorNode prev = first;
        ImTriangulatorNode temp = first;
        ImTriangulatorNode current = first.Next;
        prev.Next = prev;
        prev.Prev = prev;
        while (current != first)
        {
            temp = current.Next;
            current.Next = prev;
            prev.Prev = current;
            first.Next = current;
            current.Prev = first;
            prev = current;
            current = temp;
        }
        _Nodes[0] = prev;
    }

    bool IsEar(int i0, int i1, int i2, ImVec2 v0, ImVec2 v1, ImVec2 v2)
    {
        for (int i = 0; i < _Reflexes.Count; i++)
        {
            var reflex = _Reflexes[i];
            if (reflex.Index != i0 && reflex.Index != i1 && reflex.Index != i2)
                if (ImGuiCore.ImTriangleContainsPoint(v0, v1, v2, reflex.Pos))
                    return false;
        }
        return true;
    }

    void ReclassifyNode(ImTriangulatorNode n1)
    {
        ImTriangulatorNodeType type;
        ImTriangulatorNode n0 = n1.Prev;
        ImTriangulatorNode n2 = n1.Next;
        if (!ImGuiCore.ImTriangleIsClockwise(n0.Pos, n1.Pos, n2.Pos))
            type = ImTriangulatorNodeType.ImTriangulatorNodeType_Reflex;
        else if (IsEar(n0.Index, n1.Index, n2.Index, n0.Pos, n1.Pos, n2.Pos))
            type = ImTriangulatorNodeType.ImTriangulatorNodeType_Ear;
        else
            type = ImTriangulatorNodeType.ImTriangulatorNodeType_Convex;

        if (type == n1.Type)
            return;
        if (n1.Type == ImTriangulatorNodeType.ImTriangulatorNodeType_Reflex)
            _Reflexes.RemoveAll(n => n.Index == n1.Index);
        else if (n1.Type == ImTriangulatorNodeType.ImTriangulatorNodeType_Ear)
            _Ears.RemoveAll(n => n.Index == n1.Index);
        if (type == ImTriangulatorNodeType.ImTriangulatorNodeType_Reflex)
            _Reflexes.Add(n1);
        else if (type == ImTriangulatorNodeType.ImTriangulatorNodeType_Ear)
            _Ears.Add(n1);
        n1.Type = type;
    }
}
