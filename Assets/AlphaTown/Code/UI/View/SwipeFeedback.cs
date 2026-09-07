using System.Collections.Generic;
using UnityEngine;

namespace AlphaTown.UI.View
{
    /// <summary>
    /// The dust behind the blade and the burst over a cut crop, built from pooled sprites.
    ///
    /// A <see cref="TrailRenderer"/> and a <see cref="ParticleSystem"/> would be the obvious
    /// answer, and the sickle keeps serialized slots for both so real VFX can be dropped in. Both
    /// need a material, and a material needs a shader that survives Android build stripping — so
    /// the placeholder that has to work on a phone today is made of <see cref="SpriteRenderer"/>s,
    /// which bring their own. It is the same bargain <see cref="PlaceholderArt"/> already makes:
    /// something legible now, thrown away the day there is art.
    ///
    /// Everything is pooled and nothing allocates after the first few swipes, because this runs on
    /// the one gesture the player makes hundreds of times per session.
    /// </summary>
    public sealed class SwipeFeedback
    {
        /// <summary>Enough for a long swipe plus a burst or two mid-flight.</summary>
        const int Capacity = 64;

        const float TrailLifeSeconds = 0.32f;
        const float BurstLifeSeconds = 0.55f;

        sealed class Puff
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public Color Tint;
            public float Age;
            public float Life;
            public float StartScale;
            public float EndScale;
            public float Spin;
        }

        readonly List<Puff> _pool = new List<Puff>(Capacity);
        readonly Transform _parent;
        readonly int _sortingOrder;

        Vector3 _lastTrailPoint;
        bool _hasTrailPoint;

        public SwipeFeedback(Transform parent, int sortingOrder)
        {
            _parent = parent;
            _sortingOrder = sortingOrder;
        }

        /// <summary>Chaff kicked up along the swipe. Rate-limited by distance, not by frame.</summary>
        public void TrailPoint(Vector3 world)
        {
            const float spacing = 0.22f;

            if (_hasTrailPoint && (world - _lastTrailPoint).sqrMagnitude < spacing * spacing) return;

            _lastTrailPoint = world;
            _hasTrailPoint = true;

            var puff = Take();
            if (puff == null) return;

            // Drifting mostly upward and slightly backward, so the dust hangs where the blade was
            // rather than chasing the finger.
            Launch(puff, world,
                velocity: new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.25f, 0.8f), 0f),
                tint: new Color(0.94f, 0.88f, 0.62f, 0.75f),
                life: TrailLifeSeconds,
                startScale: Random.Range(0.16f, 0.26f),
                endScale: 0.02f);
        }

        /// <summary>The pop over a cut crop: the one bit of feedback that says the swipe landed.</summary>
        public void HarvestBurst(Vector3 world)
        {
            const int count = 7;

            var offset = Random.Range(0f, 360f);

            for (var i = 0; i < count; i++)
            {
                var puff = Take();
                if (puff == null) return;

                var degrees = offset + i * (360f / count) + Random.Range(-12f, 12f);
                var radians = degrees * Mathf.Deg2Rad;
                var speed = Random.Range(1.1f, 2.2f);

                Launch(puff, world,
                    velocity: new Vector3(Mathf.Cos(radians) * speed, Mathf.Sin(radians) * speed * 0.7f + 0.9f, 0f),
                    tint: new Color(0.98f, 0.82f, 0.36f, 0.95f),
                    life: BurstLifeSeconds,
                    startScale: Random.Range(0.20f, 0.34f),
                    endScale: 0.04f);
            }
        }

        /// <summary>Starts a fresh swipe, so the first point is not spaced against the last one's end.</summary>
        public void BeginSwipe() => _hasTrailPoint = false;

        public void Tick(float deltaTime)
        {
            const float gravity = -3.2f;

            for (var i = 0; i < _pool.Count; i++)
            {
                var puff = _pool[i];
                if (puff.Life <= 0f) continue;

                puff.Age += deltaTime;

                if (puff.Age >= puff.Life)
                {
                    Retire(puff);
                    continue;
                }

                var t = puff.Age / puff.Life;

                puff.Velocity.y += gravity * deltaTime;
                puff.Transform.position += puff.Velocity * deltaTime;
                puff.Transform.Rotate(0f, 0f, puff.Spin * deltaTime);

                var scale = Mathf.Lerp(puff.StartScale, puff.EndScale, t);
                puff.Transform.localScale = new Vector3(scale, scale, 1f);

                // Fading on a square keeps the puff solid for most of its life and then goes
                // quickly, which reads as dust settling instead of a light being dimmed.
                var fade = 1f - t;
                puff.Renderer.color = new Color(puff.Tint.r, puff.Tint.g, puff.Tint.b, puff.Tint.a * fade * fade);
            }
        }

        /// <summary>Hides everything at once, for when the tool is put away mid-swipe.</summary>
        public void Clear()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].Life > 0f) Retire(_pool[i]);
            }

            _hasTrailPoint = false;
        }

        void Launch(Puff puff, Vector3 world, Vector3 velocity, Color tint, float life,
                    float startScale, float endScale)
        {
            world.z = 0f;

            puff.Transform.position = world;
            puff.Transform.localScale = new Vector3(startScale, startScale, 1f);
            puff.Transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            puff.Velocity = velocity;
            puff.Tint = tint;
            puff.Age = 0f;
            puff.Life = life;
            puff.StartScale = startScale;
            puff.EndScale = endScale;
            puff.Spin = Random.Range(-220f, 220f);

            puff.Renderer.color = tint;
            puff.Renderer.enabled = true;
        }

        static void Retire(Puff puff)
        {
            puff.Life = 0f;
            puff.Renderer.enabled = false;
        }

        /// <summary>
        /// The oldest free puff, or a new one while the pool is still growing. Returns null once
        /// the pool is full and every puff is alive — dropping one is better than a frame spike
        /// during the exact gesture this exists to make feel good.
        /// </summary>
        Puff Take()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].Life <= 0f) return _pool[i];
            }

            if (_pool.Count >= Capacity) return null;

            var go = new GameObject("Puff");
            go.transform.SetParent(_parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderArt.Puff();
            renderer.sortingOrder = _sortingOrder;
            renderer.enabled = false;

            var puff = new Puff { Transform = go.transform, Renderer = renderer };
            _pool.Add(puff);
            return puff;
        }
    }
}
