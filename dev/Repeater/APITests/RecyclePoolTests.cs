﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Windows.UI.Xaml.Tests.MUXControls.ApiTests.RepeaterTests.Common;
using Windows.UI.Xaml.Tests.MUXControls.ApiTests.RepeaterTests.Common.Mocks;
using MUXControlsTestApp.Utilities;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Common;

#if USING_TAEF
using WEX.TestExecution;
using WEX.TestExecution.Markup;
using WEX.Logging.Interop;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
#endif

#if !BUILD_WINDOWS
using ItemsRepeater = Microsoft.UI.Xaml.Controls.ItemsRepeater;
using RecyclePool = Microsoft.UI.Xaml.Controls.RecyclePool;
using StackLayout = Microsoft.UI.Xaml.Controls.StackLayout;
using ScrollAnchorProvider = Microsoft.UI.Xaml.Controls.ScrollAnchorProvider;
#endif

namespace Windows.UI.Xaml.Tests.MUXControls.ApiTests.RepeaterTests
{
    [TestClass]
    public class RecyclePoolTests : TestsBase
    {

        public void ValidateElementsHaveCorrectKeys()
        {
            RunOnUIThread.Execute(() =>
            {
                var element = new Button();
                const string buttonKey = "ButtonKey";
                const string textBlockKey = "TextBlockKey";
                const string stackPanelKey = "StackPanelKey";

                RecyclePool pool = new RecyclePool();
                pool.PutElement(new Button(), buttonKey);
                pool.PutElement(new TextBlock(), textBlockKey);
                pool.PutElement(new StackPanel(), stackPanelKey);
                pool.PutElement(new Button(), buttonKey);
                pool.PutElement(new TextBlock(), textBlockKey);
                pool.PutElement(new StackPanel(), stackPanelKey);
                pool.PutElement(new Button(), buttonKey);
                pool.PutElement(new TextBlock(), textBlockKey);
                pool.PutElement(new StackPanel(), stackPanelKey);

                Verify.IsNotNull((Button)pool.TryGetElement(buttonKey));
                Verify.IsNotNull((Button)pool.TryGetElement(buttonKey));
                Verify.IsNotNull((Button)pool.TryGetElement(buttonKey));
                Verify.IsNull(pool.TryGetElement(buttonKey));

                Verify.IsNotNull((TextBlock)pool.TryGetElement(textBlockKey));
                Verify.IsNotNull((TextBlock)pool.TryGetElement(textBlockKey));
                Verify.IsNotNull((TextBlock)pool.TryGetElement(textBlockKey));
                Verify.IsNull(pool.TryGetElement(textBlockKey));

                Verify.IsNotNull((StackPanel)pool.TryGetElement(stackPanelKey));
                Verify.IsNotNull((StackPanel)pool.TryGetElement(stackPanelKey));
                Verify.IsNotNull((StackPanel)pool.TryGetElement(stackPanelKey));
                Verify.IsNull(pool.TryGetElement(stackPanelKey));


                Verify.Throws<COMException>(delegate
                {
                    pool.PutElement(new Button(), null, null);
                });

                Verify.Throws<COMException>(delegate
                {
                    pool.PutElement(new Button(), buttonKey, new Button() /* not a panel */);
                });

                Verify.Throws<COMException>(delegate
                {
                    pool.TryGetElement(null, null);
                });
            });
        }

        [TestMethod]
        public void ValidateOwnershipWithStackPanel()
        {
            RunOnUIThread.Execute(() =>
            {
                RecyclePool pool = new RecyclePool();
                var owner = new StackPanel();
                var child = new Button();
                owner.Children.Add(child);
                pool.PutElement(child, "Key", owner);
                var recycled = pool.TryGetElement("Key", owner);
                Verify.AreSame(child, recycled);
                Verify.AreEqual(0, owner.Children.IndexOf(child));
            });
        }

        // Validate that if the pool has an element for the requested owner,
        // then that is given preference over other elements.
        [TestMethod]
        public void ValidateRecycledElementOwnerAffinity()
        {
            RunOnUIThread.Execute(() =>
            {
                ItemsRepeater repeater1 = null;
                ItemsRepeater repeater2 = null;
                const int numItems = 10;
                var dataCollection = new ObservableCollection<int>(Enumerable.Range(0, numItems));
                const string recycleKey = "key";

                var dataSource = MockItemsSource.CreateDataSource<int>(dataCollection, true);
                var layout = new StackLayout();
                var recyclePool = new RecyclePool();
                var itemTemplate = (DataTemplate)XamlReader.Load(
                    @"<DataTemplate  xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                         <TextBlock Text='{Binding}' />
                    </DataTemplate>");

                repeater1 = new ItemsRepeater()
                {
                    ItemsSource = dataSource,
                    Layout = layout,
#if BUILD_WINDOWS
                    ItemTemplate = (Windows.UI.Xaml.IElementFactory)new RecyclingElementFactoryDerived()
#else
                    ItemTemplate = new RecyclingElementFactoryDerived()
#endif
                    {
                        Templates = { { "key", itemTemplate } },
                        RecyclePool = recyclePool,
                        SelectTemplateIdFunc = (object data, UIElement owner) => recycleKey
                    }
                };

                repeater2 = new ItemsRepeater()
                {
                    ItemsSource = dataSource,
                    Layout = layout,
#if BUILD_WINDOWS
                    ItemTemplate = (Windows.UI.Xaml.IElementFactory)new RecyclingElementFactoryDerived()
#else
                    ItemTemplate = new RecyclingElementFactoryDerived()
#endif
                    {
                        Templates = { { "key", itemTemplate } },
                        RecyclePool = recyclePool,
                        SelectTemplateIdFunc = (object data, UIElement owner) => recycleKey
                    }
                };

                var root = new StackPanel();
                root.Children.Add(repeater1);
                root.Children.Add(repeater2);

                Content = new ScrollAnchorProvider()
                {
                    Width = 400,
                    Height = 400,
                    Content = new ScrollViewer()
                    {
                        Content = root
                    }
                };

                Content.UpdateLayout();
                Verify.AreEqual(numItems, VisualTreeHelper.GetChildrenCount(repeater1));
                Verify.AreEqual(numItems, VisualTreeHelper.GetChildrenCount(repeater2));

                // Throw all the elements into the recycle pool
                dataCollection.Clear();
                Content.UpdateLayout();

                for (int i = 0; i < numItems; i++)
                {
                    var element1 = (FrameworkElement)recyclePool.TryGetElement(recycleKey, repeater1);
                    Verify.AreSame(repeater1, element1.Parent);

                    var element2 = (FrameworkElement)recyclePool.TryGetElement(recycleKey, repeater2);
                    Verify.AreSame(repeater2, element2.Parent);
                }
            });
        }
    }
}
