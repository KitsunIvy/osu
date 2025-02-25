﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.UserInterface
{
    public partial class OsuMenu : Menu
    {
        // todo: this shouldn't be required after https://github.com/ppy/osu-framework/issues/4519 is fixed.
        private bool wasOpened;

        [Resolved]
        private OsuMenuSamples menuSamples { get; set; } = null!;

        public OsuMenu(Direction direction, bool topLevelMenu = false)
            : base(direction, topLevelMenu)
        {
            BackgroundColour = Color4.Black.Opacity(0.5f);

            MaskingContainer.CornerRadius = 4;
            ItemsContainer.Padding = new MarginPadding(5);

            OnSubmenuOpen += _ => { menuSamples?.PlaySubOpenSample(); };
        }

        protected override void Update()
        {
            base.Update();

            bool showCheckboxes = false;

            foreach (var drawableItem in ItemsContainer)
            {
                if (drawableItem.Item is StatefulMenuItem)
                    showCheckboxes = true;
            }

            foreach (var drawableItem in ItemsContainer)
            {
                if (drawableItem is DrawableOsuMenuItem osuItem)
                    osuItem.ShowCheckbox.Value = showCheckboxes;
            }
        }

        protected override void AnimateOpen()
        {
            if (!TopLevelMenu && !wasOpened)
                menuSamples?.PlayOpenSample();

            this.FadeIn(300, Easing.OutQuint);
            wasOpened = true;
        }

        protected override void AnimateClose()
        {
            if (!TopLevelMenu && wasOpened)
                menuSamples?.PlayCloseSample();

            this.FadeOut(300, Easing.OutQuint);
            wasOpened = false;
        }

        protected override void UpdateSize(Vector2 newSize)
        {
            if (Direction == Direction.Vertical)
            {
                Width = newSize.X;
                this.ResizeHeightTo(newSize.Y, 300, Easing.OutQuint);
            }
            else
            {
                Height = newSize.Y;
                this.ResizeWidthTo(newSize.X, 300, Easing.OutQuint);
            }
        }

        protected override DrawableMenuItem CreateDrawableMenuItem(MenuItem item)
        {
            switch (item)
            {
                case StatefulMenuItem stateful:
                    return new DrawableStatefulMenuItem(stateful);

                case OsuMenuItemSpacer spacer:
                    return new DrawableSpacer(spacer);
            }

            return new DrawableOsuMenuItem(item);
        }

        protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new OsuScrollContainer(direction);

        protected override Menu CreateSubMenu() => new OsuMenu(Direction.Vertical)
        {
            Anchor = Direction == Direction.Horizontal ? Anchor.BottomLeft : Anchor.TopRight
        };

        protected partial class DrawableSpacer : DrawableOsuMenuItem
        {
            public DrawableSpacer(MenuItem item)
                : base(item)
            {
                Scale = new Vector2(1, 0.6f);

                AddInternal(new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = BackgroundColourHover,
                    RelativeSizeAxes = Axes.X,
                    Height = 2f,
                    Width = 0.9f,
                });
            }

            protected override bool OnHover(HoverEvent e) => true;

            protected override bool OnClick(ClickEvent e) => true;
        }
    }
}
