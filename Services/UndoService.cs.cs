using System.Collections.Generic;

namespace VectorEditor.Services
{
    /// <summary>
    /// Provides a simplified undo history system.
    /// Stores a limited number of serialized canvas states
    /// and allows stepping backward in time (Undo).
    /// Redo is intentionally not implemented as per project requirement.
    /// </summary>
    public class UndoService
    {
        /// <summary>
        /// Maximum number of stored states = max undo steps + 1 (current state).
        /// </summary>
        private readonly int _capacity;

        /// <summary>
        /// The timeline storing serialized canvas states.
        /// index 0   = oldest state
        /// index N-1 = newest state
        /// </summary>
        private readonly List<string> _timeline = new();

        /// <summary>
        /// Current position in the timeline.
        /// `_index` moves backward when Undo() is called.
        /// </summary>
        private int _index = -1;

        /// <summary>
        /// Initializes the undo system.
        /// maxSteps = how many undo steps should be allowed (e.g., 5).
        /// Internally we store maxSteps + 1 states to include the current state.
        /// </summary>
        public UndoService(int maxSteps = 5)
        {
            // Prevent invalid values and ensure at least 1 undo step.
            int steps = maxSteps < 1 ? 1 : maxSteps;

            // Capacity = allowed undo steps + the current active state.
            _capacity = steps + 1;
        }

        /// <summary>
        /// Saves a new state to the timeline.
        /// - Does NOT save duplicate consecutive states (unless force = true).
        /// - Clears any forward states (redo data) when a new state is added.
        /// - Ensures the total state count never exceeds the capacity.
        /// </summary>
        /// <param name="state">Serialized canvas state.</param>
        /// <param name="force">If true, saves even if it matches the current state.</param>
        public void Save(string state, bool force = false)
        {
            // Prevent adding duplicates unless forced.
            if (!force && _index >= 0 && _timeline[_index] == state)
                return;

            // If we previously undid steps, remove the "future" states.
            if (_index < _timeline.Count - 1)
                _timeline.RemoveRange(_index + 1, _timeline.Count - (_index + 1));

            // Append the new state.
            _timeline.Add(state);

            // Update current index to the newest state.
            _index = _timeline.Count - 1;

            // Enforce capacity limit (drop oldest states if necessary).
            while (_timeline.Count > _capacity)
            {
                _timeline.RemoveAt(0);
                _index--; // Adjust index because the timeline shifted left.
            }
        }

        /// <summary>
        /// Moves one step back in the timeline.
        /// Returns the previous stored state, or null if no undo is available.
        /// </summary>
        /// <returns>Serialized previous state or null.</returns>
        public string Undo()
        {
            if (_index > 0)
            {
                _index--;
                return _timeline[_index];
            }
            return null; // Nothing to undo
        }

        /// <summary>
        /// Resets the entire timeline to a single initial state.
        /// Typically used after loading a project from a file.
        /// </summary>
        /// <param name="initialState">The canvas state to replace all history with.</param>
        public void ResetWith(string initialState)
        {
            _timeline.Clear();
            _timeline.Add(initialState);
            _index = 0;
        }
    }
}
