// Port of imstb_rectpack.h (skyline packer) - exact algorithm so glyph UV coordinates match the C++ build.
using System;
using System.Collections.Generic;

namespace ImGui;

public static class StbRectPack
{
    public const int STBRP_HEURISTIC_Skyline_default = 0;
    public const int STBRP_HEURISTIC_Skyline_BL_sortHeight = 0;
    public const int STBRP_HEURISTIC_Skyline_BF_sortHeight = 1;
    const int STBRP__MAXVAL = 0xffff;

    public class stbrp_node
    {
        public stbrp_node next;
        public int x;
        public int y;
    }

    public struct stbrp_rect
    {
        public int id;
        public int w, h;
        public int x, y;
        public int was_packed;
    }

    class stbrp__findresult
    {
        public stbrp_node prev_link;    // node whose 'next' link must be updated (or null = fail)
        public bool ok;
        public bool insert_at_head;
        public int x, y;
    }

    public class stbrp_context
    {
        public int width, height, align, init_mode;
        public int heuristic;
        public int num_nodes;
        public stbrp_node[] nodes;      // node storage
        public stbrp_node active_head;  // pointer emulation: active list head
        stbrp_node _extra0, _extra1;    // context->extra[2]
        public stbrp_node extra0, extra1;
        stbrp_node free_head;

        public void init_target(int width, int height, stbrp_node[] nodes, int num_nodes)
        {
            this.nodes = nodes;
            for (int i = 0; i < num_nodes - 1; ++i)
                nodes[i].next = nodes[i + 1];
            if (num_nodes > 0)
                nodes[num_nodes - 1].next = null;
            init_mode = STBRP__INIT_skyline;
            heuristic = STBRP_HEURISTIC_Skyline_default;
            free_head = nodes[0];
            extra0 = new stbrp_node();
            extra1 = new stbrp_node();
            active_head = extra0;
            this.width = width;
            this.height = height;
            this.num_nodes = num_nodes;
            setup_allow_out_of_mem(false);

            // node 0 is the full width, node 1 is the sentinel
            extra0.x = 0;
            extra0.y = 0;
            extra0.next = extra1;
            extra1.x = width;
            extra1.y = (1 << 30);
            extra1.next = null;
        }

        void setup_allow_out_of_mem(bool allow_out_of_mem)
        {
            if (allow_out_of_mem)
                align = 1;
            else
                align = (width + num_nodes - 1) / num_nodes;
        }

        // find minimum y position if it starts at x1
        int skyline_find_min_y(stbrp_node first, int x0, int width_, ref int pwaste)
        {
            stbrp_node node = first;
            int x1 = x0 + width_;
            int min_y, visited_width, waste_area;

            min_y = 0;
            waste_area = 0;
            visited_width = 0;
            while (node.x < x1)
            {
                if (node.y > min_y)
                {
                    waste_area += visited_width * (node.y - min_y);
                    min_y = node.y;
                    if (node.x < x0)
                        visited_width += node.next.x - x0;
                    else
                        visited_width += node.next.x - node.x;
                }
                else
                {
                    int under_width = node.next.x - node.x;
                    if (under_width + visited_width > width_)
                        under_width = width_ - visited_width;
                    waste_area += under_width * (min_y - node.y);
                    visited_width += under_width;
                }
                node = node.next;
            }
            pwaste = waste_area;
            return min_y;
        }

        stbrp__findresult skyline_find_best_pos(int width_, int height)
        {
            int best_waste = (1 << 30), best_x, best_y = (1 << 30);
            var fr = new stbrp__findresult();
            stbrp_node prev_node = null; bool prev_is_head = false; stbrp_node best_node = null;

            // align to multiple of c->align
            width_ = (width_ + align - 1);
            width_ -= width_ % align;

            if (width_ > width || height > height_of())
            {
                fr.ok = false;
                fr.x = fr.y = 0;
                return fr;
            }

            stbrp_node node = active_head;
            prev_node = null; prev_is_head = true;
            while (node.x + width_ <= width)
            {
                int waste = 0;
                int y = skyline_find_min_y(node, node.x, width_, ref waste);
                if (heuristic == STBRP_HEURISTIC_Skyline_BL_sortHeight)
                {
                    // bottom left
                    if (y < best_y)
                    {
                        best_y = y;
                        best_node = prev_is_head ? null : prev_node;
                        best_is_head = prev_is_head;
                    }
                }
                else
                {
                    if (y + height <= height_of())
                    {
                        if (y < best_y || (y == best_y && waste < best_waste))
                        {
                            best_y = y;
                            best_waste = waste;
                            best_node = prev_is_head ? null : prev_node;
                            best_is_head = prev_is_head;
                        }
                    }
                }
                prev_node = node;
                prev_is_head = false;
                node = node.next;
            }

            // best_x = (*best)->x in C (0 if no candidate found; failure handled below)
            best_x = 0;
            if (best_is_head) best_x = active_head.x;
            else if (best_node != null) best_x = best_node.next.x;

            fr.ok = best_is_head || best_node != null;
            fr.x = best_x;
            fr.y = best_y;
            // represent 'prev_link' as the node AFTER which insertion happens:
            fr.prev_link = best_is_head ? null : best_node; // null means head when best_is_head, but ok distinguishes
            fr.insert_at_head = best_is_head;
            return fr;
        }

        bool best_is_head;

        int height_of() => height;

        stbrp__findresult skyline_pack_rectangle(int width_, int height)
        {
            var res = skyline_find_best_pos(width_, height);

            // bail if: failed, doesn't fit, or out of memory
            // (C: res.y + height > context->height — use height_of(), the parameter shadows the field)
            if ((!res.ok && !res.insert_at_head) || res.y + height > height_of() || free_head == null)
            {
                res.ok = false;
                return res;
            }

            // on success, create new node
            stbrp_node node = free_head;
            node.x = res.x;
            node.y = res.y + height;
            free_head = node.next;

            // find the insertion point ('cur' = node after insertion position)
            stbrp_node cur;
            if (res.insert_at_head)
            {
                cur = active_head;
                if (cur.x < res.x)
                {
                    stbrp_node next = cur.next;
                    cur.next = node;
                    cur = next;
                }
                else
                {
                    active_head = node;
                }
            }
            else
            {
                cur = res.prev_link.next;
                if (cur.x < res.x)
                {
                    stbrp_node next = cur.next;
                    cur.next = node;
                    cur = next;
                }
                else
                {
                    res.prev_link.next = node;
                }
            }

            // traverse cur and free the nodes, until we get to one that shouldn't be freed
            while (cur.next != null && cur.next.x <= res.x + width_)
            {
                stbrp_node next = cur.next;
                cur.next = free_head;
                free_head = cur;
                cur = next;
            }

            // stitch the list back in
            node.next = cur;

            if (cur.x < res.x + width_)
                cur.x = res.x + width_;

            return res;
        }

        public int pack_rects(stbrp_rect[] rects, int num_rects)
        {
            int all_rects_packed = 1;

            // single-rect calls are the norm in imgui; sort/unsort is a no-op then.
            // For multi-rect calls, mirror the C qsort with height comparator (unstable for ties,
            // matches MSVCRT behavior closely enough since imgui always packs 1 at a time).
            var order = new int[num_rects];
            for (int i = 0; i < num_rects; ++i)
            {
                rects[i].was_packed = i;
                order[i] = i;
            }
            if (num_rects > 1)
                Array.Sort(order, (a, b) =>
                {
                    var p = rects[a]; var q = rects[b];
                    if (p.h > q.h) return -1;
                    if (p.h < q.h) return 1;
                    return (p.w > q.w) ? -1 : (p.w < q.w ? 1 : 0);
                });

            foreach (int i in order)
            {
                if (rects[i].w == 0 || rects[i].h == 0)
                {
                    rects[i].x = rects[i].y = 0;
                }
                else
                {
                    var fr = skyline_pack_rectangle(rects[i].w, rects[i].h);
                    if (fr.ok && (fr.insert_at_head || fr.prev_link != null))
                    {
                        rects[i].x = fr.x;
                        rects[i].y = fr.y;
                    }
                    else
                    {
                        rects[i].x = rects[i].y = STBRP__MAXVAL;
                    }
                }
            }

            // set was_packed flags
            for (int i = 0; i < num_rects; ++i)
            {
                rects[i].was_packed = (rects[i].x != STBRP__MAXVAL || rects[i].y != STBRP__MAXVAL) ? 1 : 0;
                if (rects[i].was_packed == 0)
                    all_rects_packed = 0;
            }
            return all_rects_packed;
        }
    }

    const int STBRP__INIT_skyline = 1;
}
