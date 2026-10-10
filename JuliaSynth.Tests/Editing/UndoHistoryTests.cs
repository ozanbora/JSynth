using JuliaSynth.ViewModels;
using Xunit;

namespace JuliaSynth.Tests.Editing
{
    /// <summary>
    /// The history on its own, with a string standing in for the design.
    /// </summary>
    public class UndoHistoryTests
    {
        private string _design = "a";

        private UndoHistory NewHistory()
        {
            UndoHistory history = new(() => _design, state => _design = state);
            history.Reset();
            return history;
        }

        [Fact]
        public void Undo_and_redo_walk_the_recorded_steps()
        {
            Ui.Run(() =>
            {
                UndoHistory history = NewHistory();
                _design = "b"; history.Commit();
                _design = "c"; history.Commit();

                history.Undo();
                Assert.Equal("b", _design);
                history.Undo();
                Assert.Equal("a", _design);
                Assert.False(history.CanUndo);

                history.Redo();
                Assert.Equal("b", _design);
            });
        }

        [Fact]
        public void An_unchanged_design_is_not_a_step()
        {
            Ui.Run(() =>
            {
                UndoHistory history = NewHistory();
                history.Commit();
                history.Commit();

                Assert.False(history.CanUndo);
            });
        }

        [Fact]
        public void A_new_change_after_undo_drops_the_redo_steps()
        {
            Ui.Run(() =>
            {
                UndoHistory history = NewHistory();
                _design = "b"; history.Commit();
                history.Undo();

                _design = "x"; history.Commit();

                Assert.False(history.CanRedo);
                history.Undo();
                Assert.Equal("a", _design);
            });
        }

        [Fact]
        public void A_pending_change_counts_before_it_is_recorded()
        {
            Ui.Run(() =>
            {
                UndoHistory history = NewHistory();
                _design = "b";
                history.ScheduleCommit();

                Assert.True(history.IsDirty);
                Assert.True(history.CanUndo);

                // Undo records the pending change first, then steps back over it.
                history.Undo();
                Assert.Equal("a", _design);
            });
        }

        [Fact]
        public void Dirty_means_different_from_the_last_save()
        {
            Ui.Run(() =>
            {
                UndoHistory history = NewHistory();
                Assert.False(history.IsDirty);

                _design = "b"; history.Commit();
                Assert.True(history.IsDirty);

                history.MarkSaved();
                Assert.False(history.IsDirty);

                history.Undo();
                Assert.True(history.IsDirty);

                history.Redo();
                Assert.False(history.IsDirty);
            });
        }
    }
}
