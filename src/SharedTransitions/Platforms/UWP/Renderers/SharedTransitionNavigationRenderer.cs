using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Plugin.SharedTransitions;
using Plugin.SharedTransitions.Platforms.UWP;
using Xamarin.Forms;
using Xamarin.Forms.Platform.UWP;

[assembly: ExportRenderer(typeof(SharedTransitionNavigationPage), typeof(SharedTransitionNavigationRenderer))]

namespace Plugin.SharedTransitions.Platforms.UWP
{
    public class SharedTransitionNavigationRenderer : NavigationPageRenderer
    {
        public SharedTransitionNavigationRenderer()
        {

        }
    }
}
