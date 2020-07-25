using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Xamarin.Forms.Internals;
using Xamarin.Forms;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml;
using Windows.UI.Composition;

namespace Plugin.SharedTransitions.Platforms.UWP
{
    /// <summary>
    /// Set the navigation transition for the NavigationPage
    /// </summary>
    internal class NavigationTransition
    {
        public static void SetupAnimationEvents(SharedTransitionNavigationPage stnp)
        {
            stnp.PushRequested += Stnp_PushRequested;
            stnp.Pushed += Stnp_Pushed;

            stnp.PopRequested += Stnp_PopRequested;
            stnp.Popped += Stnp_Popped;

            stnp.PopToRootRequested += Stnp_PopToRootRequested;
            stnp.PoppedToRoot += Stnp_PoppedToRoot;

            stnp.RemovePageRequested += Stnp_RemovePageRequested;
            stnp.InsertPageBeforeRequested += Stnp_InsertPageBeforeRequested;
        }

        private static void Stnp_PushRequested(object sender, NavigationRequestedEventArgs e)
        {
            // setup cross-page animation for source page
            var stnp = sender as SharedTransitionNavigationPage;
            var ns = stnp.Navigation.NavigationStack;
            var tm = stnp.TransitionMap;

            var pageFrom = e.BeforePage;
            if (pageFrom == null && ns.Count >= 2)
                pageFrom = ns[ns.Count - 2];

            var pageTo = e.Page;

            // PushRequested occurs after the navigation stack has been updated
            System.Diagnostics.Debug.Assert(pageTo == ns[ns.Count - 1]);

            PrepareSharedTransition(tm, pageFrom, pageTo, false);
        }

        private static void Stnp_Pushed(object sender, NavigationEventArgs e)
        {
            // execute cross-page animation setup above to new page
            var stnp = sender as SharedTransitionNavigationPage;
            var ns = stnp.Navigation.NavigationStack;
            var tm = stnp.TransitionMap;

            Page pageFrom = null;
            if (ns.Count >= 2)
                pageFrom = ns[ns.Count - 2];

            var pageTo = ns[ns.Count - 1];

            // Pushed occurs after the navigation stack has been updated

            ExecutePreparedTransition(tm, pageFrom, pageTo, false);
        }

        private static void PrepareSharedTransition(ITransitionMapper tm, Page pageFrom, Page pageTo, bool isPop)
        {
            IReadOnlyList<TransitionDetail> transFrom = null;

            if (isPop)
            {
                // current page is pageFrom being popped; new page will be pageTo

                transFrom = tm.GetMap(pageFrom, null, isPop);
            }
            else
            {
                // current page == pageFrom; new page to push pageTo

                // group is used when pushing from a page with the group defined
                var group = (string)pageFrom?.GetValue(SharedTransitionNavigationPage.TransitionSelectedGroupProperty);

                transFrom = tm.GetMap(pageFrom, group, isPop);
            }

            // save this so that when we return to the page, we don't have to guess which items to use
            pageFrom?.SetValue(SharedTransitionNavigationPage.TransitionFromMapProperty, transFrom);

            var cas = ConnectedAnimationService.GetForCurrentView();
            if (pageFrom != null)
            {
                cas.DefaultDuration = TimeSpan.FromMilliseconds((long)pageFrom.GetValue(SharedTransitionNavigationPage.TransitionDurationProperty));
            }

            // TODO: better indication of Background items to do first...
            foreach (var bg in transFrom.Where(td => td.TransitionName.Contains("Background")))
            {
                if (bg.NativeView.IsAlive)
                {
                    var ca = cas.PrepareToAnimate(bg.TransitionName, (UIElement)bg.NativeView.Target);
                }
            }

            foreach (var detail in transFrom.Where(td => !td.TransitionName.Contains("Background")))
            {
                if (detail.NativeView.IsAlive)
                {
                    var ca = cas.PrepareToAnimate(detail.TransitionName, (UIElement)detail.NativeView.Target);
                }
            }
        }

        private static void ExecutePreparedTransition(ITransitionMapper tm, Page pageFrom, Page pageTo, bool isPop)
        {
            var cas = ConnectedAnimationService.GetForCurrentView();

            var transFrom = (IReadOnlyList<TransitionDetail>)pageFrom?.GetValue(SharedTransitionNavigationPage.TransitionFromMapProperty);
            pageFrom?.SetValue(SharedTransitionNavigationPage.TransitionFromMapProperty, null);

            // group is used when popping back to a page with the group defined
            var group = (string)pageTo?.GetValue(SharedTransitionNavigationPage.TransitionSelectedGroupProperty);
            var transTo = tm.GetMap(pageTo, group, !isPop);

            foreach (var detail in transTo)
            {
                ConnectedAnimation animation = cas.GetAnimation(detail.TransitionName);
                if (animation != null)
                {
                    if (detail.NativeView.IsAlive)
                    {
                        if (detail.View.Height == -1)
                        {
                            // need to capture the value and then use that in the event handler since it changes with each time through the loop
                            var acapture = animation;
                            detail.View.SizeChanged += (s, e) =>
                            {
                                // save this so we only call TryStart on the first time through
                                var atemp = acapture;
                                acapture = null;
                                if (atemp != null && detail.NativeView.IsAlive)
                                    atemp.TryStart((UIElement)detail.NativeView.Target);
                            };
                        }
                        else
                        {
                            var b = animation.TryStart((UIElement)detail.NativeView.Target);
                        }
                    }
                }
            }

            foreach (var transChk in transFrom ?? Enumerable.Empty<TransitionDetail>())
            {
                if (!transTo.Any(t => t.TransitionName == transChk.TransitionName))
                {
                    // transFrom item not in transTo
                    ConnectedAnimation animation = cas.GetAnimation(transChk.TransitionName);
                    if (animation != null)
                    {
                        animation.Cancel();
                    }

                    if (isPop)
                    {
                        // TODO: figure out why this is needed
                        transChk.View.IsVisible = false;
                        if (transChk.NativeView.IsAlive)
                            ((UIElement)transChk.NativeView.Target).Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private static void Stnp_PopRequested(object sender, NavigationRequestedEventArgs e)
        {
            var stnp = sender as SharedTransitionNavigationPage;
            var ns = stnp.Navigation.NavigationStack;
            var tm = stnp.TransitionMap;

            var pageFrom = e.Page;

            // PopRequested occurs before nav stack is updated
            System.Diagnostics.Debug.Assert(pageFrom == ns[ns.Count - 1]);

            Page pageTo = null;
            if (ns.Count >= 2)
                pageTo = ns[ns.Count - 2];

            PrepareSharedTransition(tm, pageFrom, pageTo, true);
        }

        private static void Stnp_Popped(object sender, NavigationEventArgs e)
        {
            var stnp = sender as SharedTransitionNavigationPage;
            var ns = stnp.Navigation.NavigationStack;
            var tm = stnp.TransitionMap;

            Page pageFrom = e.Page;
            var pageTo = ns[ns.Count - 1];

            // Popped occurs after the navigation stack has been updated

            ExecutePreparedTransition(tm, pageFrom, pageTo, true);
        }

        private static void Stnp_PopToRootRequested(object sender, NavigationRequestedEventArgs e)
        {
        }

        private static void Stnp_PoppedToRoot(object sender, NavigationEventArgs e)
        {
        }

        private static void Stnp_InsertPageBeforeRequested(object sender, NavigationRequestedEventArgs e)
        {
        }

        private static void Stnp_RemovePageRequested(object sender, NavigationRequestedEventArgs e)
        {
        }
    }
}
