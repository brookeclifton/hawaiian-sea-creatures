using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reef
{
    /// <summary>
    /// Optional "collect them into your basket" game. Off by default: to try it, tick
    /// SpeciesSelector.collectingEnabled on CC_HUB. X then adds the targeted species.
    ///
    /// This only keeps score and fires events; build any HUD, checklist or end screen on top
    /// by listening to <see cref="Changed"/>.
    /// </summary>
    public class SpeciesBasket : MonoBehaviour
    {
        [Tooltip("Species the class is asked to find. Leave empty to allow anything collectible.")]
        public List<SpeciesData> checklist = new List<SpeciesData>();

        public int ItemCount => items.Count;
        public int UniqueSpeciesCount => species.Count;
        public int Score { get; private set; }
        public IReadOnlyCollection<SpeciesData> CollectedSpecies => species;

        /// <summary>Raised whenever something is added or the basket is reset.</summary>
        public event Action<SpeciesBasket> Changed;

        readonly List<SelectableSpecies> items = new List<SelectableSpecies>();
        readonly HashSet<SpeciesData> species = new HashSet<SpeciesData>();

        public bool Has(SpeciesData d) => d != null && species.Contains(d);

        public bool ChecklistComplete
        {
            get
            {
                if (checklist == null || checklist.Count == 0) return false;
                foreach (var d in checklist) if (d != null && !species.Contains(d)) return false;
                return true;
            }
        }

        /// <summary>Add a selectable to the basket. Returns false if it can't be collected.</summary>
        public bool Add(SelectableSpecies s)
        {
            if (s == null || s.IsCollected) return false;
            var d = s.species;
            if (d != null && !d.collectible) return false;
            if (d != null && checklist != null && checklist.Count > 0 && !checklist.Contains(d)) return false;

            s.Collect();
            items.Add(s);
            if (d != null)
            {
                species.Add(d);
                Score += d.points;
            }
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Empty the basket and put every collected object back in the scene.</summary>
        public void ResetBasket()
        {
            foreach (var s in items) if (s != null) s.ResetCollected();
            items.Clear();
            species.Clear();
            Score = 0;
            Changed?.Invoke(this);
        }
    }
}
