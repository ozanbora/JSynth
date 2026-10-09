using Avalonia.Threading;
using System;
using System.Collections.Generic;

namespace JuliaSynth.ViewModels
{
    /// <summary>
    /// Undo and redo by snapshots: after each change the whole design is captured as text, and
    /// undoing restores the previous capture. Designs are small, so this is cheap, and every kind of
    /// edit (adding, moving, wiring, editing values) is covered without per-edit undo code.
    /// </summary>
    public sealed class UndoHistory
    {
        // Changes this close together (typing, a drag's row and column) become one undo step.
        private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(400);
        private const int MaxSteps = 200;

        private readonly Func<string> _capture;
        private readonly Action<string> _restore;
        private readonly List<string> _states = [];
        private readonly DispatcherTimer _timer;
        private int _index = -1;
        private bool _restoring;

        public UndoHistory(Func<string> capture, Action<string> restore)
        {
            _capture = capture;
            _restore = restore;
            _timer = new DispatcherTimer { Interval = QuietPeriod };
            _timer.Tick += (_, _) => Commit();
        }

        public event EventHandler? Changed;

        public bool CanUndo => _index > 0 || IsPending;

        public bool CanRedo => _index < _states.Count - 1 && !IsPending;

        /// <summary>
        /// The design differs from the one last saved (or loaded). Undoing back to it clears this.
        /// </summary>
        public bool IsDirty => IsPending || (_index >= 0 && _states[_index] != _savedState);

        private bool IsPending => _timer.IsEnabled;

        private string? _savedState;

        /// <summary>
        /// Forgets all steps; the current design becomes the starting point, counted as saved.
        /// </summary>
        public void Reset()
        {
            _timer.Stop();
            _states.Clear();
            _states.Add(_capture());
            _index = 0;
            _savedState = _states[0];
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The steps, where in them the design is, and which was saved: what a tab keeps of the history while
        /// another tab's design is open.
        /// </summary>
        public sealed record Steps(IReadOnlyList<string> States, int Index, string? Saved)
        {
            public bool CanUndo => Index > 0;

            public bool IsDirty => Index >= 0 && States[Index] != Saved;
        }

        /// <summary>
        /// The history so far (once anything pending is a step).
        /// </summary>
        public Steps Save()
        {
            Commit();
            return new([.. _states], _index, _savedState);
        }

        /// <summary>
        /// Back to <paramref name="steps"/>, for the design they were saved with.
        /// </summary>
        public void Restore(Steps steps)
        {
            _timer.Stop();
            _states.Clear();
            _states.AddRange(steps.States);
            _index = steps.Index;
            _savedState = steps.Saved;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The current design was just saved.
        /// </summary>
        public void MarkSaved()
        {
            Commit();
            _savedState = _index >= 0 ? _states[_index] : null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Something changed: record it once things are quiet.
        /// </summary>
        public void ScheduleCommit()
        {
            if (_restoring || _index < 0)
            {
                return;
            }

            _timer.Stop();
            _timer.Start();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Records the current design as a step, unless it is the same as the last one.
        /// </summary>
        public void Commit()
        {
            _timer.Stop();
            if (_index < 0)
            {
                return;
            }

            string state = _capture();
            if (state != _states[_index])
            {
                _states.RemoveRange(_index + 1, _states.Count - _index - 1);
                _states.Add(state);
                if (_states.Count > MaxSteps)
                {
                    // The saved state may be the one dropped; it still compares by content.
                    _states.RemoveAt(0);
                }

                _index = _states.Count - 1;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            Commit();
            if (_index > 0)
            {
                Apply(_index - 1);
            }
        }

        public void Redo()
        {
            Commit();
            if (_index < _states.Count - 1)
            {
                Apply(_index + 1);
            }
        }

        private void Apply(int index)
        {
            _index = index;
            _restoring = true;
            try
            {
                _restore(_states[index]);
            }
            finally
            {
                _restoring = false;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
