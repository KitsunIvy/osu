// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Humanizer;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Screens.Edit.Timing;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    public partial class SamplePointPiece : HitObjectPointPiece, IHasPopover
    {
        public readonly HitObject HitObject;

        public SamplePointPiece(HitObject hitObject)
        {
            HitObject = hitObject;
        }

        protected override Color4 GetRepresentingColour(OsuColour colours) => colours.Pink;

        [BackgroundDependencyLoader]
        private void load()
        {
            HitObject.DefaultsApplied += _ => updateText();
            updateText();
        }

        protected override bool OnClick(ClickEvent e)
        {
            this.ShowPopover();
            return true;
        }

        private void updateText()
        {
            Label.Text = $"{abbreviateBank(GetBankValue(GetSamples()))} {GetVolumeValue(GetSamples())}";
        }

        private static string? abbreviateBank(string? bank)
        {
            return bank switch
            {
                "normal" => "N",
                "soft" => "S",
                "drum" => "D",
                _ => bank
            };
        }

        public static string? GetBankValue(IEnumerable<HitSampleInfo> samples)
        {
            return samples.FirstOrDefault()?.Bank;
        }

        public static int GetVolumeValue(ICollection<HitSampleInfo> samples)
        {
            return samples.Count == 0 ? 0 : samples.Max(o => o.Volume);
        }

        protected virtual IList<HitSampleInfo> GetSamples() => HitObject.Samples;

        public virtual Popover GetPopover() => new SampleEditPopover(HitObject);

        public partial class SampleEditPopover : OsuPopover
        {
            private readonly HitObject hitObject;

            private LabelledTextBox bank = null!;
            private IndeterminateSliderWithTextBoxInput<int> volume = null!;

            private FillFlowContainer togglesCollection = null!;

            protected virtual IList<HitSampleInfo> GetSamples(HitObject ho) => ho.Samples;

            [Resolved(canBeNull: true)]
            private EditorBeatmap beatmap { get; set; } = null!;

            public SampleEditPopover(HitObject hitObject)
            {
                this.hitObject = hitObject;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                FillFlowContainer flow;

                Children = new Drawable[]
                {
                    flow = new FillFlowContainer
                    {
                        Width = 200,
                        Direction = FillDirection.Vertical,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(0, 10),
                        Children = new Drawable[]
                        {
                            togglesCollection = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(5, 5),
                            },
                            bank = new LabelledTextBox
                            {
                                Label = "Bank Name",
                            },
                            volume = new IndeterminateSliderWithTextBoxInput<int>("Volume", new BindableInt(100)
                            {
                                MinValue = 0,
                                MaxValue = 100,
                            })
                        }
                    }
                };

                bank.TabbableContentContainer = flow;
                volume.TabbableContentContainer = flow;

                // if the piece belongs to a currently selected object, assume that the user wants to change all selected objects.
                // if the piece belongs to an unselected object, operate on that object alone, independently of the selection.
                var relevantObjects = (beatmap.SelectedHitObjects.Contains(hitObject) ? beatmap.SelectedHitObjects : hitObject.Yield()).ToArray();
                var relevantSamples = relevantObjects.Select(GetSamples).ToArray();

                // even if there are multiple objects selected, we can still display sample volume or bank if they all have the same value.
                string? commonBank = getCommonBank(relevantSamples);
                if (!string.IsNullOrEmpty(commonBank))
                    bank.Current.Value = commonBank;

                int? commonVolume = getCommonVolume(relevantSamples);
                if (commonVolume != null)
                    volume.Current.Value = commonVolume.Value;

                updateBankPlaceholderText(relevantObjects);
                bank.Current.BindValueChanged(val =>
                {
                    updateBankFor(relevantObjects, val.NewValue);
                    updateBankPlaceholderText(relevantObjects);
                });
                // on commit, ensure that the value is correct by sourcing it from the objects' samples again.
                // this ensures that committing empty text causes a revert to the previous value.
                bank.OnCommit += (_, _) => bank.Current.Value = getCommonBank(relevantSamples);

                volume.Current.BindValueChanged(val => updateVolumeFor(relevantObjects, val.NewValue));

                createStateBindables(relevantObjects);
                updateTernaryStates(relevantObjects);
                togglesCollection.AddRange(createTernaryButtons().Select(b => new DrawableTernaryButton(b) { RelativeSizeAxes = Axes.None, Size = new Vector2(40, 40) }));
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                ScheduleAfterChildren(() => GetContainingInputManager().ChangeFocus(volume));
            }

            private static string? getCommonBank(IList<HitSampleInfo>[] relevantSamples) => relevantSamples.Select(GetBankValue).Distinct().Count() == 1 ? GetBankValue(relevantSamples.First()) : null;
            private static int? getCommonVolume(IList<HitSampleInfo>[] relevantSamples) => relevantSamples.Select(GetVolumeValue).Distinct().Count() == 1 ? GetVolumeValue(relevantSamples.First()) : null;

            private void updateBankFor(IEnumerable<HitObject> objects, string? newBank)
            {
                if (string.IsNullOrEmpty(newBank))
                    return;

                beatmap.BeginChange();

                foreach (var h in objects)
                {
                    var samples = GetSamples(h);

                    for (int i = 0; i < samples.Count; i++)
                    {
                        samples[i] = samples[i].With(newBank: newBank);
                    }

                    beatmap.Update(h);
                }

                beatmap.EndChange();
            }

            private void updateBankPlaceholderText(IEnumerable<HitObject> objects)
            {
                string? commonBank = getCommonBank(objects.Select(GetSamples).ToArray());
                bank.PlaceholderText = string.IsNullOrEmpty(commonBank) ? "(multiple)" : string.Empty;
            }

            private void updateVolumeFor(IEnumerable<HitObject> objects, int? newVolume)
            {
                if (newVolume == null)
                    return;

                beatmap.BeginChange();

                foreach (var h in objects)
                {
                    var samples = GetSamples(h);

                    for (int i = 0; i < samples.Count; i++)
                    {
                        samples[i] = samples[i].With(newVolume: newVolume.Value);
                    }

                    beatmap.Update(h);
                }

                beatmap.EndChange();
            }

            #region hitsound toggles

            private readonly Dictionary<string, Bindable<TernaryState>> selectionSampleStates = new Dictionary<string, Bindable<TernaryState>>();

            private void createStateBindables(IEnumerable<HitObject> objects)
            {
                foreach (string sampleName in HitSampleInfo.AllAdditions)
                {
                    var bindable = new Bindable<TernaryState>
                    {
                        Description = sampleName.Replace("hit", string.Empty).Titleize()
                    };

                    bindable.ValueChanged += state =>
                    {
                        switch (state.NewValue)
                        {
                            case TernaryState.False:
                                removeHitSampleFor(objects, sampleName);
                                break;

                            case TernaryState.True:
                                addHitSampleFor(objects, sampleName);
                                break;
                        }
                    };

                    selectionSampleStates[sampleName] = bindable;
                }
            }

            private void updateTernaryStates(IEnumerable<HitObject> objects)
            {
                foreach ((string sampleName, var bindable) in selectionSampleStates)
                {
                    bindable.Value = SelectionHandler<HitObject>.GetStateFromSelection(objects, h => GetSamples(h).Any(s => s.Name == sampleName));
                }
            }

            private IEnumerable<TernaryButton> createTernaryButtons()
            {
                foreach ((string sampleName, var bindable) in selectionSampleStates)
                    yield return new TernaryButton(bindable, string.Empty, () => ComposeBlueprintContainer.GetIconForSample(sampleName));
            }

            private void addHitSampleFor(IEnumerable<HitObject> objects, string sampleName)
            {
                if (string.IsNullOrEmpty(sampleName))
                    return;

                beatmap.BeginChange();

                foreach (var h in objects)
                {
                    var samples = GetSamples(h);

                    // Make sure there isn't already an existing sample
                    if (samples.Any(s => s.Name == sampleName))
                        return;

                    samples.Add(h.GetSampleInfo(sampleName));
                    beatmap.Update(h);
                }

                beatmap.EndChange();
            }

            private void removeHitSampleFor(IEnumerable<HitObject> objects, string sampleName)
            {
                if (string.IsNullOrEmpty(sampleName))
                    return;

                beatmap.BeginChange();

                foreach (var h in objects)
                {
                    var samples = GetSamples(h);

                    for (int i = 0; i < samples.Count; i++)
                    {
                        if (samples[i].Name == sampleName)
                            samples.RemoveAt(i--);
                    }

                    beatmap.Update(h);
                }

                beatmap.EndChange();
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                if (e.ControlPressed || e.AltPressed || e.SuperPressed || e.ShiftPressed || !checkRightToggleFromKey(e.Key, out int rightIndex))
                    return base.OnKeyDown(e);

                var item = togglesCollection.ElementAtOrDefault(rightIndex);

                if (item is not DrawableTernaryButton button) return base.OnKeyDown(e);

                button.Button.Toggle();
                return true;
            }

            private bool checkRightToggleFromKey(Key key, out int index)
            {
                switch (key)
                {
                    case Key.W:
                        index = 0;
                        break;

                    case Key.E:
                        index = 1;
                        break;

                    case Key.R:
                        index = 2;
                        break;

                    default:
                        index = -1;
                        break;
                }

                return index >= 0;
            }

            #endregion
        }
    }
}
