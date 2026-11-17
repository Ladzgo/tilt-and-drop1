using System.Collections.Generic;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>One piece on the board. Body + upright symbol + optional overlay (anchor chain / ice).</summary>
    public sealed class BlockView
    {
        public int Id;
        public byte Cell;
        public Transform Root;      // positioned in board-local coordinates
        public Transform Body;      // squash & stretch happens here
        public SpriteRenderer BodySr, SymbolSr, OverlaySr, CrackSr;

        public void SetCell(byte c)
        {
            Cell = c;
            int kind = c & 0xF0, col = c & 0x0F;
            bool colored = Cells.IsColored(c);
            BodySr.color = c == Cells.Stone ? Palette.Stone : colored ? Palette.Block[col] : Color.magenta;
            BodySr.sprite = c == Cells.Stone ? Sprites.Circle : Sprites.RoundedSquare;
            SymbolSr.enabled = colored;
            if (colored)
            {
                SymbolSr.sprite = Sprites.Symbol(col);
                SymbolSr.color = new Color(1f, 1f, 1f, 0.9f);
            }
            OverlaySr.enabled = kind == Cells.Anchor || kind == Cells.Ice2 || kind == Cells.Ice1;
            if (kind == Cells.Anchor) { OverlaySr.sprite = Sprites.Ring; OverlaySr.color = new Color(0.92f, 0.92f, 0.96f, 0.95f); OverlaySr.transform.localScale = Vector3.one * 1.25f; }
            else if (OverlaySr.enabled) { OverlaySr.sprite = Sprites.RoundedSquare; OverlaySr.color = Palette.Ice; OverlaySr.transform.localScale = Vector3.one * 1.04f; }
            CrackSr.enabled = kind == Cells.Ice1;
        }
    }

    /// <summary>Renders a GameState and plays back settle traces with juicy animation.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float BlockScale = 0.9f;            // fraction of a cell a block covers
        const float Rsq = 0.86f;                         // RoundedSquare sprite covers 86% of its unit quad
        static readonly Vector3 BodyScale = Vector3.one * (BlockScale / Rsq);
        const float GateThickness = 0.16f;

        public Transform Root { get; private set; }   // rotates with the board
        public float Angle { get; private set; }      // degrees, accumulated (CW = -90)
        public Particles Particles;
        public Sfx Sfx;
        public bool ReducedMotion;
        public float Speed = 1f;
        public int W { get; private set; }
        public int H { get; private set; }

        readonly Dictionary<int, BlockView> blocks = new Dictionary<int, BlockView>();
        readonly Dictionary<int, SpriteRenderer> gates = new Dictionary<int, SpriteRenderer>(); // key edge*16+lane
        readonly List<GameObject> statics = new List<GameObject>();
        float lastLandHaptic;
        Vector3 basePos;

        void Awake()
        {
            Root = new GameObject("BoardRoot").transform;
            Root.SetParent(transform, false);
        }

        public Vector3 Local(int index) => new Vector3(index % W - (W - 1) * 0.5f, (H - 1) * 0.5f - index / W, 0f);

        public bool TryCellAt(Vector3 world, out int x, out int y)
        {
            var l = Root.InverseTransformPoint(world);
            x = Mathf.RoundToInt(l.x + (W - 1) * 0.5f);
            y = Mathf.RoundToInt((H - 1) * 0.5f - l.y);
            return x >= 0 && x < W && y >= 0 && y < H;
        }

        /// <summary>Fits the board into the given world-space square and centers it.</summary>
        public void Layout(Vector3 center, float squareSize)
        {
            basePos = center;
            transform.position = center;
            float s = squareSize / (Mathf.Max(W, H) + 1.1f);
            transform.localScale = Vector3.one * s;
        }

        // ---- build ------------------------------------------------------------------------
        public void Build(GameState s)
        {
            Tweener.Kill(this);
            foreach (var b in blocks.Values) Destroy(b.Root.gameObject);
            blocks.Clear();
            foreach (var g in statics) Destroy(g);
            statics.Clear();
            gates.Clear();
            W = s.Level.W;
            H = s.Level.H;

            var bg = MakeSprite("bg", Root, Sprites.RoundedSquare, Palette.BoardBg, -10);
            bg.transform.localScale = new Vector3((W + 0.6f) / Rsq, (H + 0.6f) / Rsq, 1f);
            statics.Add(bg.gameObject);
            for (int i = 0; i < W * H; i++)
            {
                byte c = s.Level.Cells[i];
                if (c == Cells.Wall)
                {
                    var w = MakeSprite("wall", Root, Sprites.Square, Palette.Wall, -5);
                    w.transform.localPosition = Local(i);
                    w.transform.localScale = Vector3.one;
                    statics.Add(w.gameObject);
                }
                else
                {
                    var cell = MakeSprite("cell", Root, Sprites.RoundedSquare, Palette.BoardCell, -8);
                    cell.transform.localPosition = Local(i);
                    cell.transform.localScale = Vector3.one * (0.94f / Rsq);
                    statics.Add(cell.gameObject);
                }
            }
            BuildGates(s.Level);
            for (int i = 0; i < W * H; i++)
            {
                byte c = s.Cells[i];
                if (!Cells.IsPiece(c)) continue;
                var b = MakeBlock(s.Ids[i], c);
                b.Root.localPosition = Local(i);
                blocks[b.Id] = b;
            }
            Angle = -90f * (int)s.Gravity;
            Root.localRotation = Quaternion.Euler(0, 0, Angle);
            KeepSymbolsUpright();
            transform.position = basePos;
        }

        void BuildGates(Level lv)
        {
            for (int e = 0; e < 4; e++)
            {
                var line = lv.Gates[e];
                for (int k = 0; k < line.Length; k++)
                {
                    int col = line[k];
                    if (col == 0) continue;
                    Vector3 p;
                    Vector3 scale;
                    float off = 0.5f + GateThickness * 0.5f + 0.04f;
                    switch (e)
                    {
                        case Edges.T: p = Local(k) + Vector3.up * off; scale = new Vector3(0.84f, GateThickness, 1f); break;
                        case Edges.B: p = Local((H - 1) * W + k) + Vector3.down * off; scale = new Vector3(0.84f, GateThickness, 1f); break;
                        case Edges.L: p = Local(k * W) + Vector3.left * off; scale = new Vector3(GateThickness, 0.84f, 1f); break;
                        default: p = Local(k * W + W - 1) + Vector3.right * off; scale = new Vector3(GateThickness, 0.84f, 1f); break;
                    }
                    var g = MakeSprite("gate", Root, Sprites.Square, Palette.Block[col], -4);
                    g.transform.localPosition = p;
                    g.transform.localScale = scale;
                    var sym = MakeSprite("gsym", Root, Sprites.Symbol(col), Palette.Block[col], -4);
                    Vector3 outward = (p - Local(e == Edges.T ? k : e == Edges.B ? (H - 1) * W + k : e == Edges.L ? k * W : k * W + W - 1)).normalized;
                    sym.transform.localPosition = p + outward * 0.28f;
                    sym.transform.localScale = Vector3.one * 0.55f;
                    statics.Add(g.gameObject);
                    statics.Add(sym.gameObject);
                    gates[e * 16 + k] = g;
                }
            }
        }

        BlockView MakeBlock(int id, byte c)
        {
            var root = new GameObject("piece" + id).transform;
            root.SetParent(Root, false);
            var body = new GameObject("body").transform;
            body.SetParent(root, false);
            body.localScale = BodyScale;
            var b = new BlockView
            {
                Id = id,
                Root = root,
                Body = body,
                BodySr = body.gameObject.AddComponent<SpriteRenderer>(),
                SymbolSr = MakeSprite("sym", body, Sprites.Symbol(1), Color.white, 3),
                OverlaySr = MakeSprite("ovl", body, Sprites.Ring, Color.white, 4),
                CrackSr = MakeSprite("crack", body, Sprites.Symbol(6), new Color(1, 1, 1, 0.8f), 5),
            };
            b.BodySr.sortingOrder = 2;
            b.SymbolSr.transform.localScale = Vector3.one;
            b.CrackSr.transform.localScale = Vector3.one * 1.5f;
            b.CrackSr.transform.localRotation = Quaternion.Euler(0, 0, 20f);
            b.SetCell(c);
            return b;
        }

        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void KeepSymbolsUpright()
        {
            var q = Quaternion.Euler(0, 0, -Angle);
            foreach (var b in blocks.Values) b.SymbolSr.transform.localRotation = q;
        }

        // ---- animation --------------------------------------------------------------------
        float D(float seconds) => seconds / Mathf.Max(0.5f, Speed);

        /// <summary>Animates the board rotation for one move. Returns its duration.</summary>
        public float AnimateRotation(Rot r)
        {
            float from = Angle, to = Angle + (r == Rot.CW ? -90f : 90f);
            Angle = to;
            float dur = D(ReducedMotion ? 0.18f : 0.3f);
            Sfx?.Play(Sound.Whoosh, 0.8f, r == Rot.CW ? 1.05f : 0.95f);
            Tweener.Run(dur, p =>
            {
                float a = Mathf.LerpUnclamped(from, to, p);
                Root.localRotation = Quaternion.Euler(0, 0, a);
                var q = Quaternion.Euler(0, 0, -a);
                foreach (var b in blocks.Values) b.SymbolSr.transform.localRotation = q;
            }, ReducedMotion ? (System.Func<float, float>)Ease.OutCubic : Ease.OutBack, 0f, null, this);
            if (!ReducedMotion)
            {
                var s0 = transform.localScale;
                Tweener.Run(dur, p => transform.localScale = s0 * (1f - 0.035f * Mathf.Sin(p * Mathf.PI)), Ease.Linear, 0f, () => transform.localScale = s0, this);
            }
            return dur;
        }

        float MoveTime(int cells) => D(Mathf.Sqrt(2f * cells / 55f) + 0.02f);

        /// <summary>Schedules the trace (after `delay`) and returns the total duration.</summary>
        public float PlayEvents(List<SettleEvent> ev, Dir g, float delay)
        {
            var gv = new Vector3(Dirs.DX[(int)g], -Dirs.DY[(int)g], 0f); // gravity in board-local space
            bool vertical = g == Dir.Down || g == Dir.Up;
            float passStart = delay, passEnd = delay, stagger = 0f, lastPieceTime = delay, lastExitTime = delay;
            int curPass = -1, combo = 0, exits = 0;
            var arrival = new Dictionary<int, float>();
            var lastPos = new Dictionary<int, Vector3>();

            for (int k = 0; k < ev.Count; k++)
            {
                var e = ev[k];
                if (e.Type == SettleEventType.Hammer) { Shatter(e.PieceId, delay); passStart = passEnd = delay + D(0.15f); continue; }
                if (e.Pass != curPass) { curPass = e.Pass; passStart = passEnd; stagger = 0f; arrival.Clear(); }
                if (!blocks.TryGetValue(e.PieceId, out var b)) continue;
                switch (e.Type)
                {
                    case SettleEventType.Move:
                    {
                        int dist = Mathf.Abs(e.From % W - e.To % W) + Mathf.Abs(e.From / W - e.To / W);
                        float start = passStart + stagger, dur = MoveTime(dist);
                        stagger += D(0.018f);
                        Vector3 a = Local(e.From), z = Local(e.To);
                        Tweener.Run(dur, p => b.Root.localPosition = Vector3.LerpUnclamped(a, z, p), Ease.InQuad, start, null, this);
                        float arr = start + dur;
                        arrival[e.PieceId] = arr;
                        lastPos[e.PieceId] = z;
                        lastPieceTime = arr;
                        bool exitsNext = k + 1 < ev.Count && ev[k + 1].Type == SettleEventType.Exit && ev[k + 1].PieceId == e.PieceId;
                        if (!exitsNext) Land(b, arr, vertical, dist);
                        passEnd = Mathf.Max(passEnd, arr + D(0.04f));
                        break;
                    }
                    case SettleEventType.Exit:
                    {
                        float t = arrival.TryGetValue(e.PieceId, out float at) ? at : passStart + (stagger += D(0.018f));
                        var p0 = lastPos.TryGetValue(e.PieceId, out var lp) ? lp : b.Root.localPosition;
                        ExitAnim(b, e.Edge, e.Lane, gv, t, combo++, p0);
                        exits++;
                        blocks.Remove(e.PieceId);
                        lastExitTime = t;
                        passEnd = Mathf.Max(passEnd, t + D(0.16f));
                        break;
                    }
                    case SettleEventType.Hit:
                        HitAnim(b, e.Hp, lastPieceTime);
                        break;
                    case SettleEventType.Release:
                        ReleaseAnim(b, lastExitTime);
                        break;
                }
            }
            if (exits >= 4 && !ReducedMotion) Shake(lastExitTime, 0.12f + 0.02f * exits);
            return passEnd - delay;
        }

        void Land(BlockView b, float at, bool vertical, int dist)
        {
            float amt = Mathf.Clamp01(0.06f + 0.025f * dist) * (ReducedMotion ? 0.3f : 1f);
            var s0 = BodyScale;
            Tweener.Run(D(0.2f), p =>
            {
                float k = Mathf.Sin(p * Mathf.PI) * (1f - p);
                float sx = vertical ? 1f + amt * k * 2f : 1f - amt * k * 2f;
                float sy = vertical ? 1f - amt * k * 2f : 1f + amt * k * 2f;
                b.Body.localScale = new Vector3(s0.x * sx, s0.y * sy, 1f);
            }, Ease.Linear, at, () => b.Body.localScale = s0, this);
            Tweener.Run(0.0001f, _ => { }, Ease.Linear, at, () =>
            {
                Sfx?.Play(Sound.Land, Mathf.Clamp01(0.35f + 0.1f * dist), Random.Range(0.92f, 1.08f));
                if (Time.unscaledTime - lastLandHaptic > 0.06f) { Haptics.Play(Haptic.Light); lastLandHaptic = Time.unscaledTime; }
                if (Particles != null && b.Root != null) Particles.Puff(b.Root.position, Color.white, 0.5f * transform.localScale.x);
            }, this);
        }

        void ExitAnim(BlockView b, int edge, int lane, Vector3 gv, float at, int combo, Vector3 p0)
        {
            gates.TryGetValue(edge * 16 + lane, out var gate);
            Tweener.Run(0.0001f, _ => { }, Ease.Linear, at, () =>
            {
                Sfx?.Exit(combo);
                Haptics.Play(combo >= 3 ? Haptic.Heavy : Haptic.Medium);
                var col = Palette.Block[b.Cell & 0x0F];
                if (Particles != null) Particles.Burst(Root.TransformPoint(p0 + gv * 0.6f), col, 10 + 3 * Mathf.Min(combo, 5), 2.4f * transform.localScale.x, 0.25f * transform.localScale.x, 0.55f, Root.TransformDirection(gv) * 0.6f);
                if (gate != null)
                {
                    var gs = gate.transform.localScale;
                    Tweener.Run(D(0.25f), q =>
                    {
                        float k = Mathf.Sin(q * Mathf.PI);
                        gate.transform.localScale = gs * (1f + 0.5f * k);
                        gate.color = Color.Lerp(col, Color.white, k * 0.7f);
                    }, Ease.Linear, 0f, () => { gate.transform.localScale = gs; gate.color = col; }, this);
                }
            }, this);
            var s0 = b.Body.localScale;
            Tweener.Run(D(0.15f), p =>
            {
                if (b.Root == null) return;
                b.Root.localPosition = p0 + gv * (0.9f * p);
                b.Body.localScale = s0 * (1f - 0.8f * p);
                b.BodySr.color = new Color(b.BodySr.color.r, b.BodySr.color.g, b.BodySr.color.b, 1f - p);
                b.SymbolSr.color = new Color(1f, 1f, 1f, 0.9f * (1f - p));
            }, Ease.InQuad, at, () => { if (b.Root != null) Destroy(b.Root.gameObject); }, this);
        }

        void HitAnim(BlockView b, int hp, float at)
        {
            Tweener.Run(0.0001f, _ => { }, Ease.Linear, at, () =>
            {
                Sfx?.Play(Sound.Crack);
                Haptics.Play(Haptic.Medium);
                if (Particles != null) Particles.Burst(b.Root.position, Palette.Ice, 8, 1.8f * transform.localScale.x, 0.18f * transform.localScale.x);
                b.SetCell(hp == 1 ? (byte)(Cells.Ice1 | (b.Cell & 0x0F)) : (byte)(Cells.Block | (b.Cell & 0x0F)));
                Punch(b, 0.15f);
            }, this);
        }

        void ReleaseAnim(BlockView b, float at)
        {
            Tweener.Run(0.0001f, _ => { }, Ease.Linear, at, () =>
            {
                Sfx?.Play(Sound.Chain);
                b.SetCell((byte)(Cells.Block | (b.Cell & 0x0F)));
                Punch(b, 0.2f);
            }, this);
        }

        void Punch(BlockView b, float amount)
        {
            var s0 = BodyScale;
            Tweener.Run(D(0.25f), p => b.Body.localScale = s0 * (1f + amount * Mathf.Sin(p * Mathf.PI) * (1f - p)), Ease.Linear, 0f, () => b.Body.localScale = s0, this);
        }

        void Shatter(int id, float at)
        {
            if (!blocks.TryGetValue(id, out var b)) return;
            blocks.Remove(id);
            Tweener.Run(0.0001f, _ => { }, Ease.Linear, at, () =>
            {
                Sfx?.Play(Sound.Hammer);
                Haptics.Play(Haptic.Heavy);
                if (Particles != null) Particles.Burst(b.Root.position, b.BodySr.color, 18, 3f * transform.localScale.x, 0.26f * transform.localScale.x);
                Destroy(b.Root.gameObject);
            }, this);
        }

        void Shake(float at, float strength)
        {
            Tweener.Run(D(0.3f), p =>
            {
                float k = (1f - p) * strength * transform.localScale.x;
                transform.position = basePos + new Vector3(Mathf.Sin(p * 60f) * k, Mathf.Cos(p * 47f) * k, 0f);
            }, Ease.Linear, at, () => transform.position = basePos, this);
        }

        /// <summary>Win flourish: remaining board pulses.</summary>
        public void Celebrate()
        {
            var s0 = transform.localScale;
            Tweener.Run(0.5f, p => transform.localScale = s0 * (1f + 0.05f * Mathf.Sin(p * Mathf.PI)), Ease.Linear, 0f, () => transform.localScale = s0, this);
        }

        public void Nudge()
        {
            // invalid action feedback
            Tweener.Run(0.25f, p => transform.position = basePos + Vector3.right * Mathf.Sin(p * 30f) * 0.06f * (1f - p), Ease.Linear, 0f, () => transform.position = basePos, this);
        }
    }
}
