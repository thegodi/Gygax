﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using GygaxCore.DataStructures;
using GygaxCore.Ifc;
using GygaxCore.Interfaces;
using GygaxCore.Processors;
using GygaxVisu.Helpers;
using GygaxVisu.Visualizer;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using Application = System.Windows.Application;
using Binding = System.Windows.Data.Binding;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using UserControl = System.Windows.Controls.UserControl;

namespace GygaxVisu.Controls
{
    /// <summary>
    /// Interaction logic for PointcloudControl.xaml
    /// </summary>
    public partial class Control3D : UserControl
    {
        private IStreamable _streamableObject;

        private bool _controlHidden;

        public IStreamable StreamableObject
        {
            get
            {
                return _streamableObject;
            }
            set
            {
                _streamableObject = value;
                if(_streamableObject != null)
                    _streamableObject.PropertyChanged += OnPropertyChanged;
            }
        }

        public Control3D()
        {
            InitializeComponent();

            Viewport.RenderTechniquesManager = new DefaultRenderTechniquesManager();
            Viewport.RenderTechnique = Viewport.RenderTechniquesManager.RenderTechniques[DefaultRenderTechniqueNames.Blinn];
            Viewport.EffectsManager = new DefaultEffectsManager(Viewport.RenderTechniquesManager);
            
            SetBinding(DataContextProperty, new Binding());
            
            SizeChanged += OnSizeChanged;

            InitializeContextMenu();

            CheckIfModelHasArrived();
        }

        public void CheckIfModelHasArrived()
        {
            if (_streamableObject != null)
            {
                ViewModel();
            }
            else
            {
                System.Threading.Timer timer = null;
                timer = new System.Threading.Timer((obj) =>
                {
                    CheckIfModelHasArrived();
                    timer.Dispose();
                },
                null, 1000, System.Threading.Timeout.Infinite);
            }
        }

        private void InitializeContextMenu()
        {
            ContextMenu = new ContextMenu();

            var hideItem = new MenuItem()
            {
                Header = "Minimise"
            };

            hideItem.Click += delegate (object sender, RoutedEventArgs args)
            {
                _controlHidden = !_controlHidden;

                if (_controlHidden)
                {
                    Viewport.Visibility = Visibility.Collapsed;
                    Label.Visibility = Visibility.Visible;
                }
                else
                {
                    Viewport.Visibility = Visibility.Visible;
                    Label.Visibility = Visibility.Collapsed;
                }
                hideItem.IsChecked = _controlHidden;
            };

            ContextMenu.Items.Add(hideItem);

            var RecordItem = new MenuItem
            {
                Header = "Save"
            };

            RecordItem.Click += delegate (object sender, RoutedEventArgs args)
            {
                Save((IStreamable)DataContext);
            };

            ContextMenu.Items.Add(RecordItem);

            var InfoItem = new MenuItem
            {
                Header = "Info"
            };

            ContextMenu.Opened += delegate (object sender, RoutedEventArgs args)
            {
                InfoItem.Items.Clear();

                try
                {
                    InfoItem.Items.Add(new MenuItem
                    {
                        Header = "Name: " + ((IStreamable)DataContext).Name
                    });

                    InfoItem.Items.Add(new MenuItem
                    {
                        Header = "Location: " + ((IStreamable)DataContext).Location
                    });
                }
                catch (Exception) { }

            };

            ContextMenu.Items.Add(InfoItem);

            var CloseItem = new MenuItem
            {
                Header = "Close"
            };

            CloseItem.Click += delegate (object sender, RoutedEventArgs args)
            {
                ((IStreamable)DataContext).Close();
            };

            ContextMenu.Items.Add(CloseItem);
        }

        private void Save(IStreamable sender)
        {
            if (sender is Pointcloud)
            {
                var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.FileName = "*";
                saveFileDialog.DefaultExt = "pcd";
                saveFileDialog.ValidateNames = true;

                saveFileDialog.Filter = "Pointcloud File (.pcd)|*.pcd";

                DialogResult result = saveFileDialog.ShowDialog();

                if (!(result == DialogResult.OK)) // Test result.
                {
                    return;
                }

                sender.Save(saveFileDialog.FileName);
            }
            else if (sender is IfcViewerWrapper)
            {
                var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.FileName = "*";
                saveFileDialog.DefaultExt = "obj";
                saveFileDialog.ValidateNames = true;

                saveFileDialog.Filter = "Wavefront Obj File (.obj)|*.obj";

                DialogResult result = saveFileDialog.ShowDialog();

                if (!(result == DialogResult.OK)) // Test result.
                {
                    return;
                }
                
                var objExporter = new WavefrontObjWriter();
                objExporter.Export(IfcVisualizer.GetItems((IfcViewerWrapper)sender.Data,false),saveFileDialog.FileName,"",false);
                
            }
        }

        public void InitViewport()
        {
            Viewport.Items.Clear();
            Viewport.Reset();
            Viewport.ReAttach();

            var model = new AmbientLight3D {Color = new Color4(1, 1, 1, 1)};

            if (Viewport.RenderHost != null && Viewport.RenderHost.RenderTechnique != null)
                model.Attach(Viewport.RenderHost);
            Viewport.Items.Add(model);

            var model2 = new DirectionalLight3D {Direction = new Vector3(1, 1, 1)};
            if (Viewport.RenderHost != null && Viewport.RenderHost.RenderTechnique != null)
                model2.Attach(Viewport.RenderHost);
            Viewport.Items.Add(model2);

            var model3 = new DirectionalLight3D { Direction = new Vector3(-1, -1, -1) };
            if (Viewport.RenderHost != null && Viewport.RenderHost.RenderTechnique != null)
                model3.Attach(Viewport.RenderHost);

            Viewport.Items.Add(model3);
            
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            if (((Control3D) sender).ActualWidth > 300)
            {
                Viewport.ShowCoordinateSystem = true;
                Viewport.ShowViewCube = true;
                Viewport.IsEnabled = true;
                ContextMenu = null;
            }
            else
            {
                Viewport.ShowCoordinateSystem = false;
                Viewport.ShowViewCube = false;
                Viewport.IsEnabled = false;
                InitializeContextMenu();
            }
        }

        private void ViewModel()
        {
            Application.Current.Dispatcher.Invoke(delegate
            {
                InitViewport();

                var vis = Visualizer.Visualizer.GetModels(_streamableObject.Data);

                if (vis == null)
                    return;

                foreach (var m in vis)
                {
                    var model = m;

                    if (model.Parent != null)
                    {
                        if (model is MeshGeometryModel3D)
                        {
                            model = new MeshGeometryModel3D()
                            {
                                Geometry = ((MeshGeometryModel3D)m).Geometry,
                                Material = ((MeshGeometryModel3D)m).Material,
                                Transform = ((MeshGeometryModel3D)m).Transform
                            };
                        }
                        else if (model is PointGeometryModel3D)
                        {
                            model = new PointGeometryModel3D()
                            {
                                Geometry = ((PointGeometryModel3D)m).Geometry,
                                Transform = ((PointGeometryModel3D)m).Transform,
                                Color = ((PointGeometryModel3D)m).Color
                            };
                        }
                        else if (model is LineGeometryModel3D)
                        {
                            model = new LineGeometryModel3D()
                            {
                                Geometry = ((LineGeometryModel3D)m).Geometry,
                                Transform = ((LineGeometryModel3D)m).Transform,
                                Color = ((LineGeometryModel3D)m).Color
                            };
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (Viewport.RenderHost.RenderTechnique != null)
                        model.Attach(Viewport.RenderHost);

                    Viewport.Items.Add(model);
                }

                ViewportHelper.ZoomExtents(Viewport);

                Viewport.Visibility = Visibility.Visible;
                LoadingAnimation.Visibility = Visibility.Hidden;
            });
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "UseGlobalCamera":

                    break;

                case "Data":
                case "DataContext":
                    ViewModel();
                    break;
            }


        }

        public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(
            "DataContext",
            typeof(Object),
            typeof(Control3D),
            new PropertyMetadata(DataContextChanged)
        );

        public event PropertyChangedEventHandler PropertyChanged;

        private static void DataContextChanged(object sender,DependencyPropertyChangedEventArgs e)
        {
            Control3D myControl = (Control3D)sender;

            if (e.NewValue == null)
                return;
            
            myControl.StreamableObject = e.NewValue as IStreamable;

            if (myControl.StreamableObject.Data != null)
            {
                myControl.OnPropertyChanged(myControl.StreamableObject.Data,
                    new PropertyChangedEventArgs("DataContext"));
            }
        }
    }
}
