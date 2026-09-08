// Port of Dear ImGui containers (imgui.h ImVector<>, ImChunkStream, ImGuiStorage; imgui_internal.h ImPool, ImSpan, ImSpanAllocator).
using System;
using System.Collections.Generic;

namespace ImGui;

// ImVector: lightweight growable array mirroring C++ ImVector semantics.
// C# note: growth invalidates previous element identity for struct T, same as C++ realloc.
public class ImVector<T>
{
    public static readonly T[] EmptyData = new T[0];
    public T[] Data = EmptyData;
    public int Size;
    public int Capacity;

    public T this[int i] { get => Data[i]; set => Data[i] = value; }
    public T this[uint i] { get => Data[i]; set => Data[i] = value; }
    public void clear() { Size = 0; }
    public bool empty() => Size == 0;
    public int size() => Size;
    public void push_back(T v)
    {
        if (Size == Capacity) grow(Size + 1);
        Data[Size++] = v;
    }
    public void pop_back() { Size--; Data[Size] = default; }
    public T back() => Data[Size - 1];
    public T front() => Data[0];
    public ref T back_ref() => ref Data[Size - 1];
    public void resize(int new_size)
    {
        if (new_size > Capacity) grow(new_size);
        for (int i = Size; i < new_size; i++) Data[i] = default;
        Size = new_size;
    }
    public void resize(int new_size, T fill)
    {
        if (new_size > Capacity) grow(new_size);
        for (int i = Size; i < new_size; i++) Data[i] = fill;
        Size = new_size;
    }
    public void reserve(int new_cap)
    {
        if (new_cap <= Capacity) return;
        grow(new_cap);
    }
    public void insert(int idx, T v)
    {
        if (Size == Capacity) grow(Size + 1);
        Array.Copy(Data, idx, Data, idx + 1, Size - idx);
        Data[idx] = v;
        Size++;
    }
    public void erase(int idx)
    {
        Array.Copy(Data, idx + 1, Data, idx, Size - idx - 1);
        Size--;
        Data[Size] = default;
    }
    public void erase_unsorted(int idx) { Data[idx] = Data[Size - 1]; Size--; Data[Size] = default; }
    public int find_index(T v, IEqualityComparer<T> cmp = null)
    {
        var c = cmp ?? EqualityComparer<T>.Default;
        for (int i = 0; i < Size; i++) if (c.Equals(Data[i], v)) return i;
        return -1;
    }
    public bool contains(T v, IEqualityComparer<T> cmp = null) => find_index(v, cmp) != -1;
    public int index_of_ref(T o)
    {
        for (int i = 0; i < Size; i++) if (object.ReferenceEquals(Data[i], o)) return i;
        return -1;
    }
    public int find_index_of_ref(T o) => index_of_ref(o);
    public void push_front(T v)
    {
        if (Size == 0) { push_back(v); return; }
        grow(Size + 1);
        Array.Copy(Data, 0, Data, 1, Size);
        Data[0] = v;
        Size++;
    }

    private void grow(int min_cap)
    {
        int new_cap = Capacity == 0 ? 8 : Capacity + Capacity / 2;
        if (new_cap < min_cap) new_cap = min_cap;
        var nd = new T[new_cap];
        Array.Copy(Data, nd, Size);
        Data = nd;
        Capacity = new_cap;
    }
}

// ImSpan: span over contiguous storage
public struct ImSpan<T>
{
    public T[] Data;
    public int Offset;
    public int Count;

    public ImSpan(T[] data, int offset, int count) { Data = data; Offset = offset; Count = count; }
    public T this[int i] { get => Data[Offset + i]; set => Data[Offset + i] = value; }
    public int size() => Count;
}

// ImSpanAllocator: assists allocating spans into a single buffer
public class ImSpanAllocator<T>
{
    int _CurrOff = 0;
    int[] _Offsets;
    int _BaseOffset;

    public ImSpanAllocator(int n) { _Offsets = new int[n]; }
    public void Reserve(int n, int sz) { _Offsets[n] = _CurrOff; _CurrOff += sz; }
    public int GetArenaSize() => _CurrOff;
    public void SetArenaBaseOffset(int base_off) { _BaseOffset = base_off; }
    public int GetSpanOffsetBegin(int n) => _BaseOffset + _Offsets[n];
    public int GetSpanSize(int n) => (n + 1 < _Offsets.Length ? _Offsets[n + 1] - _Offsets[n] : _CurrOff - _Offsets[n]);
}

// ImPool: basic keyed storage with free-list reuse (imgui_internal.h)
// C# note: T is a class; dead slots are simply not referenced by the map.
public class ImPool<T> where T : class, new()
{
    public ImVector<T> Buf = new ImVector<T>();     // Contiguous data
    public ImGuiStorage Map = new ImGuiStorage();   // ID->Index
    public int FreeIdx;                             // Next free idx to use
    public int AliveCount;                          // Number of active/alive items
    int[] _FreeNext = new int[0];                   // parallel free-list (replaces C++ storing int in dead slots)

    public T GetByKey(uint key) { int idx = Map.GetInt(key, -1); return (idx != -1) ? Buf[idx] : null; }
    public T GetByIndex(int n) => Buf[n];
    public int GetIndex(T p) => Buf.index_of_ref(p);
    public bool Contains(T p) => Buf.index_of_ref(p) != -1;
    public T GetOrAddByKey(uint key)
    {
        ref int idx = ref Map.GetIntRef(key, -1);
        if (idx != -1) return Buf[idx];
        idx = FreeIdx;
        return Add();
    }
    public void Clear() { Map.Clear(); Buf.clear(); FreeIdx = AliveCount = 0; }
    public T Add()
    {
        int idx = FreeIdx;
        if (idx == Buf.Size)
        {
            Buf.resize(Buf.Size + 1);
            if (_FreeNext.Length < Buf.Capacity) Array.Resize(ref _FreeNext, Buf.Capacity);
            FreeIdx++;
        }
        else
        {
            FreeIdx = _FreeNext[idx];
        }
        Buf[idx] = new T();
        AliveCount++;
        return Buf[idx];
    }
    public void Remove(uint key, int idx)
    {
        _FreeNext[idx] = FreeIdx;
        FreeIdx = idx;
        Map.SetInt(key, -1);
        AliveCount--;
    }
    public void Remove(uint key, T p) => Remove(key, GetIndex(p));
    public void Reserve(int capacity) => Buf.reserve(capacity);

    public int GetAliveCount() => AliveCount;
    public int GetBufSize() => Buf.Size;
    public int GetMapSize() => Map.Data.Size;
    public T TryGetMapData(int n)
    {
        int idx = Map.Data[n].val_i;
        if (idx == -1) return null;
        return GetByIndex(idx);
    }
}

// ImGuiStoragePair + ImGuiStorage: key->value storage, kept sorted by key (imgui.h)
public struct ImGuiStoragePair
{
    public uint key;
    public int val_i;
    public float val_f;
    public object val_p;
    public ImGuiStoragePair(uint _key, int _val) { key = _key; val_i = _val; val_f = 0; val_p = null; }
    public ImGuiStoragePair(uint _key, float _val) { key = _key; val_i = 0; val_f = _val; val_p = null; }
    public ImGuiStoragePair(uint _key, object _val) { key = _key; val_i = 0; val_f = 0; val_p = _val; }
}

public class ImGuiStorage
{
    public ImVector<ImGuiStoragePair> Data = new ImVector<ImGuiStoragePair>();

    public void Clear() => Data.clear();

    int LowerBound(uint key)
    {
        int lo = 0, hi = Data.Size;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (Data[mid].key < key) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    // - Get...() functions find pair, never add/allocate
    public int GetInt(uint key, int default_val = 0)
    {
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return Data[idx].val_i;
        return default_val;
    }
    public bool GetBool(uint key, bool default_val = false) => GetInt(key, default_val ? 1 : 0) != 0;
    public float GetFloat(uint key, float default_val = 0.0f)
    {
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return Data[idx].val_f;
        return default_val;
    }
    public object GetVoidPtr(uint key)
    {
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return Data[idx].val_p;
        return null;
    }

    // - Get...Ref() functions find pair, add on demand if missing
    public ref int GetIntRef(uint key, int default_val = 0)
    {
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return ref Data.Data[idx].val_i;
        var p = new ImGuiStoragePair(key, default_val);
        Data.insert(idx, p);
        return ref Data.Data[idx].val_i;
    }
    public ref bool GetBoolRef(uint key, bool default_val = false)
    {
        // Reuse int storage; booleans stored as 0/1.
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return ref UnsafeAsBool(ref Data.Data[idx].val_i);
        var p = new ImGuiStoragePair(key, default_val ? 1 : 0);
        Data.insert(idx, p);
        return ref UnsafeAsBool(ref Data.Data[idx].val_i);
    }
    static ref bool UnsafeAsBool(ref int i) => ref System.Runtime.CompilerServices.Unsafe.As<int, bool>(ref i);

    public ref float GetFloatRef(uint key, float default_val = 0.0f)
    {
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return ref Data.Data[idx].val_f;
        var p = new ImGuiStoragePair(key, default_val);
        Data.insert(idx, p);
        return ref Data.Data[idx].val_f;
    }
    public ref object GetVoidPtrRef(uint key, object default_val = null)
    {
        int idx = LowerBound(key);
        if (idx < Data.Size && Data[idx].key == key) return ref Data.Data[idx].val_p;
        var p = new ImGuiStoragePair(key, default_val);
        Data.insert(idx, p);
        return ref Data.Data[idx].val_p;
    }

    // - Set...() functions call Get...Ref() then assign
    public void SetInt(uint key, int val) => GetIntRef(key) = val;
    public void SetBool(uint key, bool val) => GetIntRef(key, 0) = val ? 1 : 0;
    public void SetFloat(uint key, float val) => GetFloatRef(key) = val;
    public void SetVoidPtr(uint key, object val) => GetVoidPtrRef(key) = val;

    public void SetAllInt(int val) { for (int i = 0; i < Data.Size; i++) Data.Data[i].val_i = val; }

    public void BuildSortByKey()
    {
        var pairs = new ImGuiStoragePair[Data.Size];
        Array.Copy(Data.Data, pairs, Data.Size);
        Array.Sort(pairs, (a, b) => a.key.CompareTo(b.key));
        Array.Copy(pairs, Data.Data, pairs.Length);
    }
}

// ImChunkStream: stream of variable-sized chunks with 4-byte header storing the chunk size.
// C# port stores byte chunks; T is decoded by user code. Used by settings storage.
public class ImChunkStream
{
    public ImVector<byte> Buf = new ImVector<byte>();

    public void clear() => Buf.clear();
    public bool empty() => Buf.Size == 0;
    public int size() => Buf.Size;

    public int alloc_chunk(int sz)
    {
        const int HDR_SZ = 4;
        int aligned = (HDR_SZ + sz + 3) & ~3;
        int off = Buf.Size;
        Buf.resize(off + aligned);
        WriteInt(off, aligned);
        return off + HDR_SZ;
    }
    public int begin() => Buf.Size == 0 ? 0 : 4;
    public int end() => Buf.Size;
    public int chunk_size(int p) => ReadInt(p - 4);
    public int next_chunk(int p)
    {
        p += chunk_size(p);
        if (p == Buf.Size) return 0;
        return p;
    }
    public void WriteInt(int off, int v)
    {
        Buf.Data[off] = (byte)v; Buf.Data[off + 1] = (byte)(v >> 8);
        Buf.Data[off + 2] = (byte)(v >> 16); Buf.Data[off + 3] = (byte)(v >> 24);
    }
    public int ReadInt(int off) => Buf.Data[off] | (Buf.Data[off + 1] << 8) | (Buf.Data[off + 2] << 16) | (Buf.Data[off + 3] << 24);
}

// ImChunkStream<T>: C++ ImChunkStream<T> stores placement-new'd T records with per-chunk offsets.
// The C# port keeps owned T objects with stable indices standing in for offsets.
public class ImChunkStream<T> where T : class
{
    public ImVector<T> Buf = new ImVector<T>();

    public T this[int i] => Buf[i];
    public void clear() => Buf.clear();
    public bool empty() => Buf.empty();
    public int size() => Buf.Size;
    public T alloc_chunk(T obj) { Buf.push_back(obj); return obj; }
    public int offset_from_ptr(T obj) => Buf.index_of_ref(obj);
    public T ptr_from_offset(int offset) => Buf[offset];
}
