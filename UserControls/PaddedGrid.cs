using System;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;
using System.ComponentModel;

namespace ZipImageViewer
{
    /// <summary>
    /// The PaddedGrid control is a Grid that supports padding.
    /// </summary>
    public class PaddedGrid : Grid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaddedGrid"/> class.
        /// </summary>
        public PaddedGrid()
        {
            //  Add a loded event handler.
            Loaded += new RoutedEventHandler(PaddedGrid_Loaded);
        }

        /// <summary>
        /// Handles the Loaded event of the PaddedGrid control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> instance containing the event data.</param>
        void PaddedGrid_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (UIElement child in this.Children)
            {
                // FrameworkElement introduces the MarginProperty
                if (child is FrameworkElement)
                {
                    // Bind the child's margin to the grid's padding.
                    BindingOperations.SetBinding(child, FrameworkElement.MarginProperty, new Binding("Padding") { Source = this });

                    // Bind the child's alignments to the grid's ChildrenAlignments if it is not set.
                    if (child.ReadLocalValue(HorizontalAlignmentProperty) == DependencyProperty.UnsetValue)
                        BindingOperations.SetBinding(child, HorizontalAlignmentProperty, new Binding("HorizontalChildrenAlignment") { Source = this });
                    if (child.ReadLocalValue(VerticalAlignmentProperty) == DependencyProperty.UnsetValue)
                        BindingOperations.SetBinding(child, VerticalAlignmentProperty, new Binding("VerticalChildrenAlignment") { Source = this });
                }
            }
        }

        /// <summary>
        /// Called when the padding changes.
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <param name="args">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnPaddingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            //  Get the padded grid that has had its padding changed.
            PaddedGrid paddedGrid = dependencyObject as PaddedGrid;

            //  Force the layout to be updated.
            paddedGrid.UpdateLayout();
        }

        /// <summary>
        /// The internal dependency property object for the 'Padding' property.
        /// </summary>
        private static readonly DependencyProperty PaddingProperty =
          DependencyProperty.Register("Padding", typeof(Thickness), typeof(PaddedGrid),
          new UIPropertyMetadata(new Thickness(0.0), new PropertyChangedCallback(OnPaddingChanged)));

        /// <summary>
        /// Gets or sets the padding.
        /// </summary>
        /// <value>The padding.</value>
        [Description("The padding property."), Category("Common Properties")]
        public Thickness Padding
        {
            get { return (Thickness)GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }



        public HorizontalAlignment HorizontalChildrenAlignment
        {
            get { return (HorizontalAlignment)GetValue(HorizontalChildrenAlignmentProperty); }
            set { SetValue(HorizontalChildrenAlignmentProperty, value); }
        }

        public static readonly DependencyProperty HorizontalChildrenAlignmentProperty =
            DependencyProperty.Register("HorizontalChildrenAlignment", typeof(HorizontalAlignment), typeof(PaddedGrid), new PropertyMetadata(HorizontalAlignment.Stretch));



        public VerticalAlignment VerticalChildrenAlignment
        {
            get { return (VerticalAlignment)GetValue(VerticalChildrenAlignmentProperty); }
            set { SetValue(VerticalChildrenAlignmentProperty, value); }
        }

        public static readonly DependencyProperty VerticalChildrenAlignmentProperty =
            DependencyProperty.Register("VerticalChildrenAlignment", typeof(VerticalAlignment), typeof(PaddedGrid), new PropertyMetadata(VerticalAlignment.Center));
    }
}
