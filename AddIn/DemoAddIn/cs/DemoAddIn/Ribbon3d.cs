﻿using SolidEdgeCommunity.AddIn;
using SolidEdgeCommunity.Extensions; // https://github.com/SolidEdgeCommunity/SolidEdge.Community/wiki/Using-Extension-Methods
using SolidEdgeFramework;
using SolidEdgeFrameworkSupport;
using SolidEdgePart;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;

namespace DemoAddIn
{
    class Ribbon3d : SolidEdgeCommunity.AddIn.Ribbon,
         SolidEdgeFramework.ISEMouseEvents // Solid Edge Mouse Events
    {
        const string _embeddedResourceName = "DemoAddIn.Ribbon3d.xml";
        private RibbonButton _buttonBoundingBox;
        private RibbonButton _buttonOpenGlBoxes;
        private RibbonButton _buttonGdiPlus;
        private RibbonButton _buttonHole;
        private RibbonButton _buttonCutout;
        private RibbonButton _buttonSlot;
        private SolidEdgeCommunity.ConnectionPointController _connectionPointController;
        private static readonly HttpClient _client = new HttpClient();
        private static bool _getting_suggestions = false;
        private static SolidEdgeFramework.Command _cmd = null;
        private static SolidEdgeFramework.Mouse _mouse = null;
        private static SolidEdgeFramework.Application _application = null;
        private static SolidEdgeGeometry.Plane _plane = null;
        private  MatchCollection matchCollection = null;
        private int count = 0;
        private static double[] Match;
        private bool checkhole = false;
        private bool checkcutout = false;
        private bool checkslot = false;
       

        public Ribbon3d()
        {
            // Get a reference to the current assembly. This is where the ribbon XML is embedded.
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            // In this example, XML file must have a build action of "Embedded Resource".
            this.LoadXml(assembly, _embeddedResourceName);

            // Example of how to bind a local variable to a particular ribbon control.
            _buttonBoundingBox = GetButton(20);
            _buttonOpenGlBoxes = GetButton(21);
            _buttonGdiPlus = GetButton(22);
            _buttonHole = GetButton(4);
            _buttonCutout = GetButton(5);
            _buttonSlot = GetButton(6);

            // Example of how to bind a particular ribbon control click event.
            _buttonBoundingBox.Click += _buttonBoundingBox_Click;
            _buttonOpenGlBoxes.Click += _buttonOpenGlBoxes_Click;
            _buttonGdiPlus.Click += _buttonGdiPlus_Click;
            _buttonHole.Click += _buttonHole_Click;
            _buttonCutout.Click += _buttoncutout_Click;
            _buttonSlot.Click += _buttonSlot_Click;

            // Get the Solid Edge version.
            var version = DemoAddIn.Instance.SolidEdgeVersion;
            _application = DemoAddIn.Instance.Application;
            // Create an instance of the default connection point controller. It helps manage connections to COM events.
            _connectionPointController = new SolidEdgeCommunity.ConnectionPointController(this);

            // View.GetModelRange() is only available in ST6 or greater.
            if (version.Major < 106)
            {
                _buttonBoundingBox.Enabled = false;
            }
        }

        public override void OnControlClick(RibbonControl control)
        {
            // Demonstrate how to handle commands without binding to a local variable.
            switch (control.CommandId)
            {
                case 0:
                    using (var dialog = new SaveFileDialog())
                    {
                        // The ShowDialog() extension method is exposed by:
                        // using SolidEdgeFramework.Extensions (SolidEdge.Community.dll)
                        if (_application.ShowDialog(dialog) == DialogResult.OK)
                        {

                        }
                    }
                    break;
                case 1:
                    using (var dialog = new FolderBrowserDialog())
                    {
                        // The ShowDialog() extension method is exposed by:
                        // using SolidEdgeFramework.Extensions (SolidEdge.Community.dll)
                        if (_application.ShowDialog(dialog) == DialogResult.OK)
                        {
                        }
                    }
                    break;
                case 2:
                    using (var dialog = new MyCustomDialog())
                    {
                        // The ShowDialog() extension method is exposed by:
                        // using SolidEdgeFramework.Extensions (SolidEdge.Community.dll)
                        if (_application.ShowDialog(dialog) == DialogResult.OK)
                        {
                        }
                    }
                    break;
                case 8:
                    _application.StartCommand(SolidEdgeConstants.PartCommandConstants.PartToolsOptions);
                    break;
                case 11:
                    _application.StartCommand(SolidEdgeConstants.PartCommandConstants.PartHelpSolidEdgeontheWeb);
                    break;
            }
        }

        void _buttonGdiPlus_Click(RibbonControl control)
        {
            var overlay = GetActiveOverlay();

            // Toggle the button check state.
            _buttonGdiPlus.Checked = !_buttonGdiPlus.Checked;
            overlay.ShowGDIPlus = _buttonGdiPlus.Checked;
        }

        void _buttonOpenGlBoxes_Click(RibbonControl control)
        {
            var overlay = GetActiveOverlay();

            // Toggle the button check state.
            _buttonOpenGlBoxes.Checked = !_buttonOpenGlBoxes.Checked;
            overlay.ShowOpenGlBoxes = _buttonOpenGlBoxes.Checked;
        }

        void _buttonBoundingBox_Click(RibbonControl control)
        {
            var overlay = GetActiveOverlay();

            // Toggle the button check state.
            _buttonBoundingBox.Checked = !_buttonBoundingBox.Checked;
            overlay.ShowBoundingBox = _buttonBoundingBox.Checked;
        }

        void _buttonHole_Click(RibbonControl control)
        {
            var overlay = GetActiveOverlay();

            _buttonHole.Checked = !_buttonHole.Checked;

            ConnectMouse();
            //_application.StartCommand(SolidEdgeConstants.PartCommandConstants.PartViewLookatFace);
            
            overlay.ShowOpenHole = _buttonHole.Checked;
            checkhole = true;
            checkslot = false;
            checkcutout = false;
        }

        void _buttoncutout_Click(RibbonControl control)
        {

            var overlay = GetActiveOverlay();

            _buttonCutout.Checked = !_buttonCutout.Checked;

            ConnectMouse();

            overlay.Showcutout = _buttonCutout.Checked;
            checkhole = false;
            checkslot = false;
            checkcutout = true;

        }

        void _buttonSlot_Click(RibbonControl control)
        {
            var overlay = GetActiveOverlay();

            _buttonSlot.Checked = !_buttonSlot.Checked;

            ConnectMouse();

            overlay.ShowSlot = _buttonSlot.Checked;
            checkhole = false;
            checkslot = true;
            checkcutout = false;

        }

        private void ConnectMouse()
        {
            _cmd = (SolidEdgeFramework.Command)_application.CreateCommand((int)SolidEdgeConstants.seCmdFlag.seNoDeactivate);
            _mouse = (SolidEdgeFramework.Mouse)_cmd.Mouse;
            _cmd.Start();
            _mouse.EnabledMove = true;
            _mouse.LocateMode = (int)SolidEdgeConstants.seLocateModes.seLocateSimple;
            _mouse.ScaleMode = 1;   // Design model coordinates.
            _mouse.WindowTypes = 1; // Graphic window's only.
            _mouse.AddToLocateFilter(32);
            _connectionPointController.AdviseSink<SolidEdgeFramework.ISEMouseEvents>(_mouse);
        }

        private MyViewOverlay GetActiveOverlay()
        {
            var controlller = DemoAddIn.Instance.ViewOverlayController;
            var window = (SolidEdgeFramework.Window)DemoAddIn.Instance.Application.ActiveWindow;
            var overlay = (MyViewOverlay)controlller.GetOverlay(window);

            if (overlay == null)
            {
                // If the overlay has not been created yet, add a new one.
                overlay = controlller.Add<MyViewOverlay>(window);
            }

            return overlay;
        }

       

        async void ISEMouseEvents.MouseClick(short sButton, short sShift, double dX, double dY, double dZ, object pWindowDispatch, int lKeyPointType, object pGraphicDispatch)
        {
            if (checkhole)
            {
                try
                {

                    //MessageBox.Show($"dx{1000 * dX}, dy{1000 * dY}, dz{1000 * dZ}");
                    _getting_suggestions = true;

                    _application = SolidEdgeCommunity.SolidEdgeUtils.Connect();

                    PartDocument _doc = _application.ActiveDocument as PartDocument;
                    Model _model = _doc.Models.Item(1);
                    Holes _holes = _model.Holes;
                    var cc = _holes.Count;
                    
                    var selected_face = pGraphicDispatch as SolidEdgeGeometry.Face;



                    Array minparams = new double[2] as Array;
                    Array maxparams = new double[2] as Array;
                    selected_face.GetParamRange(ref minparams, ref maxparams);
                    var mins = minparams as double[];
                    var maxs = maxparams as double[];

                    Array u = new double[2] { mins[0] + 0.5*(maxs[0]-mins[0]),
                                      mins[1] + 0.5*(maxs[1]-mins[1])};

                    Array n = new double[3] as Array;

                    //getting the normal vector of the selected face
                    selected_face.GetNormal(1, ref u, ref n);
                    var norm = n as double[];
                    int x = (int)Math.Round(norm[0]);
                    int y = (int)Math.Round(norm[1]);
                    int z = (int)Math.Round(norm[2]);
                    int[] face_norm = new int[3]
                    {
                     x,y,z
                    };

                    string Face_normal_vector = string.Format("{0:0}{1:0}{2:0}", x, y, z);

                    //Accessing 3D mouse coordinates 
                    _mouse.PointOnGraphic(out int PointOnGraphicFlag, out double PointOnGraphic_X, out double PointOnGraphic_Y, out double PointOnGraphic_Z);
                    MessageBox.Show($"PointonGraphic {PointOnGraphicFlag}, {PointOnGraphic_X}, {PointOnGraphic_Y}, {PointOnGraphic_Z}");


                    // create_hole(PointOnGraphic_X, PointOnGraphic_Y, PointOnGraphic_Z, selected_face, face_norm, Face_normal_vector);

                    List<HoleInfo> _holeInfos = new List<HoleInfo>();

                    foreach (Hole hole in _holes)
                    {
                        HoleInfo _holeInfo = default(HoleInfo);
                        SolidEdgePart.HoleData _holedata = hole.HoleData as SolidEdgePart.HoleData;
                        _holeInfo.diameter = 1000 * _holedata.HoleDiameter;
                        Profile profile = hole.Profile as Profile;
                        Holes2d holes2d = profile.Holes2d as Holes2d;
                        Hole2d hole2d = holes2d.Item(1);

                        double x_2d, y_2d, x_3d, y_3d, z_3d;
                        hole2d.GetCenterPoint(out x_2d, out y_2d);
                        profile.Convert2DCoordinate(x_2d, y_2d, out x_3d, out y_3d, out z_3d);

                        _holeInfo.xd = x_2d * 1000;
                        _holeInfo.yd = y_2d * 1000;
                        _holeInfo.x = x_3d * 1000;
                        _holeInfo.y = y_3d * 1000;
                        _holeInfo.z = z_3d * 1000;


                        RefPlane plane = profile.Plane as RefPlane;
                        Array normals = new double[3] as Array;
                        plane.GetNormal(ref normals);

                        double[] ns = normals as double[];
                        _holeInfo.nx = ns[0];
                        _holeInfo.ny = ns[1];
                        _holeInfo.nz = ns[2];

                        _holeInfos.Add(_holeInfo);
                        // MessageBox.Show(string.Format("diam: {0:0.000} x: {1:0.000}, y: {2:0.000}, z: {3:0.000}, nx: {3:0.000}, ny: {3:0.000}, nz: {3:0.000}",
                        //                            _holeInfo.diameter, _holeInfo.x, _holeInfo.y, _holeInfo.z, _holeInfo.nx, _holeInfo.ny, _holeInfo.nz));


                    }


                    _holeInfos = _holeInfos.OrderBy(p => p.diameter).ToList();

                    string query = "http://trapezohedron.shapespace.com:9985/v1/suggestions?query={\"status\": {\"v\": [";
                    bool first = true;

                    //adding the hole diameters to query
                    foreach (HoleInfo hi in _holeInfos)
                    {
                        if (!first)
                        {
                            query += ", ";
                        }
                        first = false;
                        string add_v = String.Format("\"{0:0.0}\"", hi.diameter);
                        query += add_v;
                    }
                    query += "], \"e\": [";


                    double dist_bucket_size = 50;
                    int v_source = 0;
                    first = true;
                    foreach (HoleInfo hi_source in _holeInfos)
                    {
                        int v_dest = 0;
                        string bucket_dir_source = string.Format("{0:0.0000}{1:0.0000}{2:0.0000}", hi_source.nx, hi_source.ny, hi_source.nz);
                        // MessageBox.Show($"Source {hi_source.x}, {hi_source.y}, {hi_source.z} --- {hi_source.nx}, {hi_source.ny}, {hi_source.nz} ");
                        // MessageBox.Show($"{bucket_dir_source}");
                        foreach (HoleInfo hi_dest in _holeInfos)
                        {

                            if (v_dest > v_source)
                            {
                                //MessageBox.Show($"destination {hi_dest.x}, {hi_dest.y}, {hi_dest.z} --- {hi_dest.nx}, {hi_dest.ny}, {hi_dest.nz}");
                                if (!first)
                                {
                                    query += ", ";
                                }
                                first = false;


                                string bucket_dir_dest = string.Format("{0:0.0000}{1:0.0000}{2:0.0000}", hi_dest.nx, hi_dest.ny, hi_dest.nz);
                                double e_dist = Math.Sqrt(Math.Pow(hi_source.x - hi_dest.x, 2) + Math.Pow(hi_source.y - hi_dest.y, 2) + Math.Pow(hi_source.z - hi_dest.z, 2));
                                //MessageBox.Show($"Bucket_dir_dest {bucket_dir_dest}, e_dist {e_dist}");
                                double e_dist_bucket = Math.Ceiling(e_dist / dist_bucket_size);
                                //MessageBox.Show($"e_dist_bucket {e_dist_bucket}");
                                string add_e = string.Format("[[\"{0:0.0}\", \"{1:0.0}\"], \"{2:0}\"]", hi_source.diameter, hi_dest.diameter, e_dist_bucket);
                                if (bucket_dir_source == bucket_dir_dest)
                                {
                                    add_e += string.Format(",[[\"{0:0.0}\", \"{1:0.0}\"], \"co_dir\"]", hi_source.diameter, hi_dest.diameter);
                                    //add_e += string.Format("[[\"{0:0.0}\", \"{1:0.0}\"], \"co_dir\"]", hi_source.diameter, hi_dest.diameter);
                                }
                                query += add_e;
                            }
                            v_dest += 1;
                        }
                        v_source += 1;
                    }

                    // query += "]}, \"location\":[[[\"32.0\", \"*\"], \"co_dir\"],";
                    query += "]}, \"location\": [";



                    first = true;
                    //Calculating distance from the mouse location to the hole center points 
                    foreach (HoleInfo H_dest in _holeInfos)
                    {
                        if (!first)
                        {
                            query += ", ";
                        }
                        first = false;

                        double e_dest = Math.Sqrt(Math.Pow(H_dest.x - (1000 * PointOnGraphic_X), 2) + Math.Pow(H_dest.y - (1000 * PointOnGraphic_Y), 2) + Math.Pow(H_dest.z - (1000 * PointOnGraphic_Z), 2));
                        double e_dist_bucket = Math.Ceiling(e_dest / dist_bucket_size);
                        string add_e = string.Format("[[\"{0:0.0}\", \"*\"], \"{1:0}\"]", H_dest.diameter, e_dist_bucket);

                        string Hole_Normal_vector = string.Format("{0:0}{1:0}{2:0}", H_dest.nx, H_dest.ny, H_dest.nz);
                        if (Hole_Normal_vector == Face_normal_vector)
                        {
                            add_e += string.Format(", [[\"{0:0.0}\", \"*\"], \"co_dir\"]", H_dest.diameter);
                            //MessageBox.Show($"2D coordinates {H_dest.xd},{H_dest.yd}");
                        }

                        query += add_e;
                    }
                    query += "]}";

                    MessageBox.Show($"{query}");

                    //string query = "http://trapezohedron.shapespace.com:9985/v1/suggestions?query={\"status\": {\"v\": [\"32.0\", \"57.0\"], \"e\": [[[\"32.0\", \"57.0\"], \"co_dir\"]]}, \"location\": [[[\"32.0\", \"*\"], \"co_dir\"]]}";
                    var values = new Dictionary<string, string> { };

                    var content = new FormUrlEncodedContent(values);
                    var response = await _client.GetAsync(query);
                    var responseString = await response.Content.ReadAsStringAsync();
                    MessageBox.Show(responseString);

                    string pattern = @"\d*\.\d";
                    matchCollection = Regex.Matches(responseString, pattern);

                    count = matchCollection.Count;
                    Match = new double[count];
                    int i = 0;
                    string match = "";

                    foreach (Match m in matchCollection)
                    {
                        match += string.Format("{0} ", m.Value);
                        Match[i] = Convert.ToDouble(m.Value);
                        i++;
                    }
                    // MessageBox.Show($"{match}");

                    create_hole(PointOnGraphic_X, PointOnGraphic_Y, PointOnGraphic_Z, selected_face, face_norm, Face_normal_vector);
                    _getting_suggestions = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            if (checkcutout)
            {
                try
                {
                    //MessageBox.Show("cutout feature selected");
                    _getting_suggestions = true;

                    _application = SolidEdgeCommunity.SolidEdgeUtils.Connect();

                    PartDocument _doc = _application.ActiveDocument as PartDocument;
                    Model _model = _doc.Models.Item(1);
                    ExtrudedCutouts _extrudedCutouts = _model.ExtrudedCutouts;
                    int a = _extrudedCutouts.Count;

                    var selected_face = pGraphicDispatch as SolidEdgeGeometry.Face;



                    Array minparams = new double[2] as Array;
                    Array maxparams = new double[2] as Array;
                    selected_face.GetParamRange(ref minparams, ref maxparams);
                    var mins = minparams as double[];
                    var maxs = maxparams as double[];

                    Array u = new double[2] { mins[0] + 0.5*(maxs[0]-mins[0]),
                                      mins[1] + 0.5*(maxs[1]-mins[1])};

                    Array n = new double[3] as Array;

                    //getting the normal vector of the selected face
                    selected_face.GetNormal(1, ref u, ref n);
                    var norm = n as double[];
                    int x = (int)Math.Round(norm[0]);
                    int y = (int)Math.Round(norm[1]);
                    int z = (int)Math.Round(norm[2]);
                    int[] face_norm = new int[3]
                    {
                     x,y,z
                    };

                    string Face_normal_vector = string.Format("{0:0}{1:0}{2:0}", x, y, z);

                    //Accessing 3D mouse coordinates 
                    _mouse.PointOnGraphic(out int PointOnGraphicFlag, out double PointOnGraphic_X, out double PointOnGraphic_Y, out double PointOnGraphic_Z);

                    List<CutoutInfo> _Cutoutinfos = new List<CutoutInfo>();

                    foreach(ExtrudedCutout extrudedCutout in _extrudedCutouts)
                    {
                        CutoutInfo _cutoutInfo = default(CutoutInfo);
                        _cutoutInfo.KeyPoints = new List<double>();

                        Profile profile = extrudedCutout.Profile as Profile;
                        SolidEdgeFrameworkSupport.Lines2d lines2D = profile.Lines2d;

                        double x_3d, y_3d, z_3d, x_3D, y_3D, z_3D;
                        int handletype;
                        SolidEdgeFramework.KeyPointType KeyPointType;

                        
                        int rc = lines2D.Count;

                        for(int j = 1; j <= rc; j++)
                        {
                            var ii = lines2D.Item(j);
                            int keycout = ii.KeyPointCount;

                            for(int i = 0; i < keycout; i++)
                            {
                                ii.GetKeyPoint(i, out x_3d, out y_3d, out z_3d, out KeyPointType, out handletype);

                                profile.Convert2DCoordinate(x_3d, y_3d, out x_3D,out y_3D,out z_3D);

                                _cutoutInfo.KeyPoints.Add(x_3D * 1000);
                                _cutoutInfo.KeyPoints.Add(y_3D * 1000);
                                _cutoutInfo.KeyPoints.Add(z_3D * 1000);


                            }
                        }
                        
                        RefPlane plane = profile.Plane as RefPlane;
                        Array normals = new double[3] as Array;
                        plane.GetNormal(ref normals);

                        //getting the normal vector of the cutout profile
                        double[] ns = normals as double[];
                        _cutoutInfo.nx = ns[0];
                        _cutoutInfo.ny = ns[1];
                        _cutoutInfo.nz = ns[2];
                       

                        _Cutoutinfos.Add(_cutoutInfo);
                    }

                    var dd =  _Cutoutinfos[0].KeyPoints[0];

                    foreach(CutoutInfo info in _Cutoutinfos)
                    {
                        //MessageBox.Show($"{Math.Round(info.nx)},{Math.Round(info.ny)},{Math.Round(info.nz)}");
                        string Cutout_normal_vector = string.Format("{0:0}{1:0}{2:0}", Math.Round(info.nx), Math.Round(info.ny), Math.Round(info.nz));
                        if (Face_normal_vector == Cutout_normal_vector)
                        {
                            MessageBox.Show("Co-dir");
                        }
                    }


                    Create_Cutout(PointOnGraphic_X, PointOnGraphic_Y, PointOnGraphic_Z, selected_face, face_norm, Face_normal_vector);


                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            if (checkslot)
            {
                try
                {
                   // MessageBox.Show("Slot feature selected");

                    _getting_suggestions = true;

                    _application = SolidEdgeCommunity.SolidEdgeUtils.Connect();

                    PartDocument _doc = _application.ActiveDocument as PartDocument;
                    Model _model = _doc.Models.Item(1);
                    Slots slots = _model.Slots;
                    int cc = slots.Count;

                    var selected_face = pGraphicDispatch as SolidEdgeGeometry.Face;

                    Array minparams = new double[2] as Array;
                    Array maxparams = new double[2] as Array;
                    selected_face.GetParamRange(ref minparams, ref maxparams);
                    var mins = minparams as double[];
                    var maxs = maxparams as double[];

                    Array u = new double[2] { mins[0] + 0.5*(maxs[0]-mins[0]),
                                      mins[1] + 0.5*(maxs[1]-mins[1])};

                    Array n = new double[3] as Array;

                    //getting the normal vector of the selected face
                    selected_face.GetNormal(1, ref u, ref n);
                    var norm = n as double[];
                    int x = (int)Math.Round(norm[0]);
                    int y = (int)Math.Round(norm[1]);
                    int z = (int)Math.Round(norm[2]);
                    int[] face_norm = new int[3]
                    {
                     x,y,z
                    };

                    string Face_normal_vector = string.Format("{0:0}{1:0}{2:0}", x, y, z);

                    //Accessing 3D mouse coordinates 
                    _mouse.PointOnGraphic(out int PointOnGraphicFlag, out double PointOnGraphic_X, out double PointOnGraphic_Y, out double PointOnGraphic_Z);

                    List<Slotinfo> _Slotinfos = new List<Slotinfo>();

                    foreach (Slot slot in slots)
                    {
                        Slotinfo _SlotInfo = default(Slotinfo);
                        _SlotInfo.KeyPoints = new List<double>();

                        Profile profile = slot.Profile as Profile;
                        Lines2d lines2D = profile.Lines2d;
                        int lincount = lines2D.Count;
                        double x_3d, y_3d, z_3d, x_3D, y_3D, z_3D;
                        int handletype;
                        KeyPointType KeyPointType;


                        int rc = lines2D.Count;

                        for (int j = 1; j <= rc; j++)
                        {
                            var ii = lines2D.Item(j);
                            int keycout = ii.KeyPointCount;

                            for (int i = 0; i < keycout; i++)
                            {
                                ii.GetKeyPoint(i, out x_3d, out y_3d, out z_3d, out KeyPointType, out handletype);

                                profile.Convert2DCoordinate(x_3d, y_3d, out x_3D, out y_3D, out z_3D);

                                _SlotInfo.KeyPoints.Add(x_3D * 1000);
                                _SlotInfo.KeyPoints.Add(y_3D * 1000);
                                _SlotInfo.KeyPoints.Add(z_3D * 1000);


                            }
                        }

                        RefPlane plane = profile.Plane as RefPlane;
                        Array normals = new double[3] as Array;
                        plane.GetNormal(ref normals);

                        //getting the normal vector of the cutout profile
                        double[] ns = normals as double[];
                        _SlotInfo.nx = ns[0];
                        _SlotInfo.ny = ns[1];
                        _SlotInfo.nz = ns[2];

                        _Slotinfos.Add(_SlotInfo);
                    }
                    var dd = _Slotinfos[0].KeyPoints[0];

                    foreach (Slotinfo info in _Slotinfos)
                    {
                        //Comparing the normal vector of the face to the slot normal vector
                        string Slot_normal_vector = string.Format("{0:0}{1:0}{2:0}", Math.Round(info.nx), Math.Round(info.ny), Math.Round(info.nz));
                        if (Face_normal_vector == Slot_normal_vector)
                        {
                            MessageBox.Show("Co-dir");
                        }
                    }

                    Create_Slot(PointOnGraphic_X, PointOnGraphic_Y, PointOnGraphic_Z, selected_face, face_norm, Face_normal_vector);
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        public double[] new_match = Match;


        async void ISEMouseEvents.MouseDown(short sButton, short sShift, double dX, double dY, double dZ, object pWindowDispatch, int lKeyPointType, object pGraphicDispatch)
        {
        }

        async void ISEMouseEvents.MouseUp(short sButton, short sShift, double dX, double dY, double dZ, object pWindowDispatch, int lKeyPointType, object pGraphicDispatch)
        {
        }

        void ISEMouseEvents.MouseMove(short sButton, short sShift, double dX, double dY, double dZ, object pWindowDispatch, int lKeyPointType, object pGraphicDispatch)
        {
        }

        void ISEMouseEvents.MouseDblClick(short sButton, short sShift, double dX, double dY, double dZ, object pWindowDispatch, int lKeyPointType, object pGraphicDispatch)
        {
        }

        void ISEMouseEvents.MouseDrag(short sButton, short sShift, double dX, double dY, double dZ, object pWindowDispatch, short DragState, int lKeyPointType, object pGraphicDispatch)
        {
        }

        //creates a hole
        private void create_hole(double PointOnGraphic_X, double PointOnGraphic_Y, double PointOnGraphic_Z,SolidEdgeGeometry.Face selected_face,
            int[] face_norm,string selected_face_normal)
        {
            
            // var selected_face = pGraphicDispatch as SolidEdgeGeometry.Face;
            PartDocument _doc = _application.ActiveDocument as PartDocument;
           
            RefPlanes refPlanes = null;
            RefPlane refPlane = null;
  
            refPlanes = _doc.RefPlanes;
            //Adding parallel refplane to the selected face 
            refPlane = refPlanes.AddParallelByDistance(selected_face, 0.0, ReferenceElementConstants.igNormalSide, false, false, true, false);

            

            //Running windows form application
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.Run(new Form1());

            MessageBox.Show("Cancel Hole Dimension?");
            Form1 form1 = new Form1();

            //Hole diameter from user input
            double cc = form1.Hole_dia;
            while(cc < 0.0)
            {
                MessageBox.Show("Enter valid dimension");
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.Run(new Form1());
                Form1 form2 = new Form1();
                double dd = form2.Hole_dia;
                MessageBox.Show("Cancel diamension?");
                if (cc == dd)
                {
                    MessageBox.Show("invalid argument");
                    dd = 0.0;   
                }
                cc = dd;
            }
             
            ProfileSets profileSets = null;
            ProfileSet profileSet = null;
            Profiles profiles = null;
            Profile profile = null;
            Models models = null;
            HoleDataCollection holeDataCollection = null;
            HoleData holeData = null;
            Holes2d holes2D = null;
            Holes holes = null;
            Sketchs sketchs = null;
            Sketch sketch = null;
            

            Array ref_dir = new double[3] as Array;

            //getting the unit vector of the reference direction
            refPlane.GetReferenceDirection(ref ref_dir);
            var Ref_dirX = ref_dir as double[];

            Array root_point = new double[3] as Array;
            refPlane.GetRootPoint(ref root_point);
            var Root_point = root_point as double[];
           
            //calculating the cross-product between ref_dir and normal vector
            double[] Ref_dirY = new double[3]
            {
                Ref_dirX[2] * face_norm[1] - Ref_dirX[1] * face_norm[2],
                Ref_dirX[0] * face_norm[2] - Ref_dirX[2] * face_norm[0],
                Ref_dirX[1] * face_norm[0] - Ref_dirX[0] * face_norm[1]
            };

            double Xcenter = -0.06; //local coordinates
            double Ycenter = -0.06;
            //calculating global coordinates from local coordinates
            double[] X_bar = new double[3]
            {
                Xcenter * Ref_dirX[0] + Ycenter * Ref_dirY[0] + Root_point[0],
                Xcenter * Ref_dirX[1] + Ycenter * Ref_dirY[1] + Root_point[1],
                Xcenter * Ref_dirX[2] + Ycenter * Ref_dirY[2] + Root_point[2]
            };

            //Calculating the angle between vectors root_point and global
            double[] OX = new double[3]
            {
                    PointOnGraphic_X - Root_point[0],
                    PointOnGraphic_Y - Root_point[1],
                    PointOnGraphic_Z - Root_point[2]
            };

            //calculating the modulus of vector OX
            double OX_Mod = Math.Sqrt(Math.Pow(OX[0], 2) + Math.Pow(OX[1], 2) + Math.Pow(OX[2], 2));

            //calculating the modulus of vector Ref_dirX
            double Ref_dirX_Mod = Math.Sqrt(Math.Pow(Ref_dirX[0], 2) + Math.Pow(Ref_dirX[1], 2) + Math.Pow(Ref_dirX[2], 2));

            //calculating the modulus of the vector ReF_dirY
            double Ref_dirY_Mod = Math.Sqrt(Math.Pow(Ref_dirY[0], 2) + Math.Pow(Ref_dirY[1], 2) + Math.Pow(Ref_dirY[2], 2));

            //calculating the dot product between vector OX and Ref_dirY
            double dotY = (OX[0] * Ref_dirY[0]) + (OX[1] * Ref_dirY[1]) + (OX[2] * Ref_dirY[2]);

            //calculating the dot product between vector OX and Ref_dirX
            double dotX = (OX[0] * Ref_dirX[0]) + (OX[1] * Ref_dirX[1]) + (OX[2] * Ref_dirX[2]);

            //calculating the angle between vector OX and Ref_dirY
            double angleY = Math.Acos(dotY / (OX_Mod * Ref_dirY_Mod));

            //calculating the angle between vector OX and Ref_dirX
            double angleX = Math.Acos(dotX / (OX_Mod * Ref_dirX_Mod));

            double X_dir = 0.0;
            double Y_dir = 0.0;
           

            if (angleY > Math.PI / 2)
            {
                X_dir = OX_Mod * Math.Cos(-angleX);
                Y_dir = OX_Mod * Math.Sin(-angleX);
            }
            else
            {
                X_dir = OX_Mod * Math.Cos(angleX);
                Y_dir = OX_Mod * Math.Sin(angleX);
            }

            if (OX_Mod == 0.0)
            {
                X_dir = 0.0;
                Y_dir = 0.0;
            }

            if (cc > 0.0)
            {
                sketchs = _doc.Sketches;
                sketch = sketchs.Add();

                holeDataCollection = _doc.HoleDataCollection;

                //Defining hole properties
                holeData = holeDataCollection.Add(
                    HoleType: SolidEdgePart.FeaturePropertyConstants.igRegularHole,
                    HoleDiameter: cc / 1000);

                profileSets = _doc.ProfileSets;
                profileSet = profileSets.Add();
                //profiles = profileSet.Profiles;
                profiles = sketch.Profiles;

                profile = profiles.Add(refPlane);
                holes2D = profile.Holes2d;

                var dd = holes2D.Add(X_dir, Y_dir);

                
                profile.End(ProfileValidationType.igProfileClosed);
                
                // dd.Move(X_dir, Y_dir, 0.0, 0.0);
                //_application.StartCommand(SolidEdgeConstants.PartCommandConstants.PartViewLookatFace);

                //getting the hole collection and creating a simple hole
                Model model = _doc.Models.Item(1);
                holes = model.Holes;
                holes.AddThroughNext(
                    Profile: profile,
                    ProfilePlaneSide: SolidEdgePart.FeaturePropertyConstants.igBoth,
                    Data: holeData);
                

            }
        }


        //creates a slot
        private void Create_Slot(double PointOnGraphic_X, double PointOnGraphic_Y, double PointOnGraphic_Z, SolidEdgeGeometry.Face selected_face,
            int[] face_norm, string selected_face_normal)
        {
            PartDocument _doc = _application.ActiveDocument as PartDocument;

            RefPlanes refPlanes = null;
            RefPlane refPlane = null;

            refPlanes = _doc.RefPlanes;
            //Adding a parallel refplane to the selected face
            refPlane = refPlanes.AddParallelByDistance(selected_face, 0.0, ReferenceElementConstants.igNormalSide, false, false, true, false);

            ProfileSets profileSets = null;
            ProfileSet profileSet = null;
            Profiles profiles = null;
            Profile profile = null;
            Lines2d lines2D = null;
            Models models = null;
            Model model = null;
            Sketchs sketchs = null;
            Sketch sketch = null;
            Slots slots = null;
            Slot slot = null;

            sketchs = _doc.Sketches;
            sketch = sketchs.Add();

            profileSets = _doc.ProfileSets;
            profileSet = profileSets.Add();
            profiles = sketch.Profiles;

            //Adding the refplane to the profile
            profile = profiles.Add(refPlane);
            lines2D = profile.Lines2d;
             
            
            lines2D.AddBy2Points(0.02, 0, 0.02, 0.02);
            
            profile.End(ProfileValidationType.igProfileClosed);

            models = _doc.Models;
            model = models.Item(1);
           
            slots = model.Slots;

            //Adding a new slot
            slots.Add(Profile: profile,
                SlotType: FeaturePropertyConstants.igRegularSlot,
                SlotEndCondition: FeaturePropertyConstants.igFormedEnd,
                SlotWidth: 0.005,
                SlotOffsetWidth: 0,
                SlotOffsetDepth: 0,
                ExtentType: FeaturePropertyConstants.igThroughAll,
                ExtentSide: FeaturePropertyConstants.igLeft,
                FiniteDistance: 0,
                KeyPointFlags: KeyPointExtentConstants.igTangentNormal,
                KeyPointOrTangentFace: null,
                ExtentType2: FeaturePropertyConstants.igNone,
                ExtentSide2: FeaturePropertyConstants.igNone,
                FiniteDistance2: 0,
                KeyPointFlags2: KeyPointExtentConstants.igTangentNormal,
                KeyPointOrTangentFace2: null,
                FromFaceOrPlane: null,
                FromOffsetSide: OffsetSideConstants.seOffsetNone,
                FromOffsetDistance: 0,
                ToFaceOrPlane: null,
                ToOffsetSide: OffsetSideConstants.seOffsetNone,
                ToOffsetDistance: 0
                );
        }


        //creates a cutout
        private void Create_Cutout(double PointOnGraphic_X, double PointOnGraphic_Y, double PointOnGraphic_Z, SolidEdgeGeometry.Face selected_face,
            int[] face_norm, string selected_face_normal)
        {
            PartDocument _doc = _application.ActiveDocument as PartDocument;

            RefPlanes refPlanes = null;
            RefPlane refPlane = null;

            refPlanes = _doc.RefPlanes;
            //Adding parallel refplane to the selected face 
            refPlane = refPlanes.AddParallelByDistance(selected_face, 0.0, ReferenceElementConstants.igNormalSide, false, false, true, false);

            Relations2d relations2D = null;
            ProfileSets profileSets = null;
            ProfileSet profileSet = null;
            Profiles profiles = null;
            Profile profile = null;
            Lines2d lines2D = null;
            Models models = null;
            Model model = null;
            Sketchs sketchs = null;
            Sketch sketch = null;
            ExtrudedCutouts extrudedCutouts = null;
           
           
            sketchs = _doc.Sketches;
            sketch = sketchs.Add();

            profileSets = _doc.ProfileSets;
            profileSet = profileSets.Add();
            profiles = sketch.Profiles;

            profile = profiles.Add(refPlane);
            lines2D = profile.Lines2d;
            relations2D = (Relations2d)profile.Relations2d;

            //adding a 2D profile for the cutout
            lines2D.AddBy2Points(0.03, -0.055, 0.045, -0.055);
            lines2D.AddBy2Points(0.045, -0.055, 0.045, -0.04);
            lines2D.AddBy2Points(0.045, -0.04, 0.03, -0.04);
            lines2D.AddBy2Points(0.03, -0.04, 0.03, -0.055);

            profile.End(ProfileValidationType.igProfileClosed);

            models = _doc.Models;
            model = models.Item(1);

            extrudedCutouts = model.ExtrudedCutouts;

            //adding a new extruded cutout
            extrudedCutouts.AddThroughNext(Profile: profile,
                ProfileSide: FeaturePropertyConstants.igLeft,
                ProfilePlaneSide: FeaturePropertyConstants.igLeft
                );
            
        }
    }
}
