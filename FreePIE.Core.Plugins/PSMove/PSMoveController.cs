using System;
using System.Collections.Generic;
using FreePIE.Core.Plugins.SensorFusion;


namespace FreePIE.Core.Plugins.PSMove
{
    /*! Extension device information *
    public struct PSMove_Ext_Device_Info
    {
        public ushort dev_id;
        public char dev_info;//[38];
    };

    /*! Boolean type. Use them instead of 0 and 1 to improve code readability. 
    public enum PSMove_Bool
    {
        PSMove_False = 0, /*!< False, Failure, Disabled (depending on context) 
        PSMove_True = 1, /*!< True, Success, Enabled (depending on context) 
    };
    /*
    public enum Extension_Device
    {
        Ext_Sharp_Shooter,
        Ext_Racing_Wheel,
        Ext_Unknown,
    };*/


    public class PSMoveController : IUpdatable
    {
        public int Index { get; set; }
        private PSMoveTracker tracker;
        private IntPtr move;

        private Vector3 position, rawPosition, centerPosition;
        private Vector3 gyroscope, accelerometer;
        private ushort exttype;
        private ExtShooter extshooter;
        private ExtWheel extwheel;
        private ExtRumble wheelrum;
        private Quaternion rotation;
        private RGB_Color led;
        private char rumble;
        private uint buttons, buttonsPressed, buttonsReleased;

        // Global holder
        public PSMoveGlobal Global { get; private set; }
        public Action OnUpdate { get; set; }
        public bool GlobalHasUpdateListener { get; set; }

        // Transitional data
        private float w, x, y, z; // for Vector3 and Quaternion components
        private char r, g, b; // for RGB_Color components

        public PSMoveController(int index, PSMoveTracker tracker)
        {
            this.Index = index;
            this.tracker = tracker;
            this.Connect();

            this.position = new Vector3();
            this.rawPosition = new Vector3();
            this.centerPosition = new Vector3();
            this.rotation = new Quaternion();
            this.gyroscope = new Vector3();
            this.accelerometer = new Vector3();
            this.led = new RGB_Color();
            this.rumble = (char)0;
            this.buttons = 0;
            this.buttonsPressed = 0;
            this.buttonsReleased = 0;
            this.exttype = (ushort)0;
            this.extshooter = new ExtShooter();
            this.extwheel = new ExtWheel();
            this.wheelrum = new ExtRumble();
            
            Global = new PSMoveGlobal(this);
        }
        #region stuff
        // **************
        // Connection
        // **************

        public bool Connect()
        {
            move = PSMoveAPI.psmove_connect_by_id(Index);
            // Enable inner psmoveapi IMU fusion
            PSMoveAPI.psmove_enable_orientation(move, 1);
            // Enable positional tracking for this device
            PSMoveAPI.psmove_tracker_enable(tracker.TrackerHandle, move);
            return isConnected();
        }

        public bool isConnected()
        {
            return (move != IntPtr.Zero);
        }

        public void Disconnect()
        {
            PSMoveAPI.psmove_disconnect(move);
        }

        // **************
        // Position
        // **************

        public Vector3 Position { 
            get {
                position = rawPosition - centerPosition;
                return position;
            }
        }

        public void resetPosition() { 
            centerPosition.x = rawPosition.x;
            centerPosition.y = rawPosition.y;
            centerPosition.z = rawPosition.z;
        }

        // **************
        // Orientation
        // **************

        public double Yaw { get { return rotation.Yaw; } }
        public double Pitch { get { return rotation.Pitch; } }
        public double Roll { get { return rotation.Roll; } }
        public Quaternion Orientation { get { return rotation; } }

        public void resetOrientation() { PSMoveAPI.psmove_reset_orientation(move); }

        public Vector3 Gyroscope { get { return gyroscope; } }
        public Vector3 Accelerometer { get { return accelerometer; } }

        public ushort ExtType { get { return exttype; } }
        public ExtShooter ExtShooter { get { return extshooter; } }
        //set { UpdateShooter(); } }
        public ExtWheel ExtWheel { get { return extwheel; } }
        //set { UpdateWheel(); } }

        // **************
        // Led and Rumble
        // **************

        public int Rumble
        {
            get
            {
                return (int)rumble;
            }
            set
            {
                rumble = RGB_Color.ClamptoChar(value);
                PSMoveAPI.psmove_set_rumble(move, rumble);
            }
        }
        /*        public ExtRumble WheelRumble
        {
            get
            {
                return (ExtRumble)wheelrum;
            }
            set
            {
                Console.WriteLine("setting rumble");
                if (ext_connected == 2)
                {
                    wheelrum = value;
                    int ruml = value.l;
                    int rumr = value.r;
                    PSMoveAPI.psmove_set_ext_wheel(move, ruml, rumr);
                    Console.WriteLine("L: " + ruml + "R: " + rumr);
                }
            }
        }*/

        public ExtRumble WheelRumble { get { return (ExtRumble)wheelrum; } }

        public RGB_Color Led { get { return led; } }

        public bool AutoLedColor
        {
            get
            {
                return (PSMoveAPI.psmove_tracker_get_auto_update_leds(tracker.TrackerHandle, move) != 0);
            }
            set
            {
                PSMoveAPI.psmove_tracker_set_auto_update_leds(tracker.TrackerHandle, move, (value) ? 1 : 0);
            }
        }

        // **************
        // Buttons
        // **************

        public bool GetButtonDown(PSMoveButton button)
        {
            return (((int)button & buttons) != 0);
        }

        public bool GetButtonUp(PSMoveButton button)
        {
            return !GetButtonDown(button);
        }

        public bool GetButtonPressed(PSMoveButton button)
        {
            return (((int)button & buttonsPressed) != 0);
        }

        public bool GetButtonReleased(PSMoveButton button)
        {
            return (((int)button & buttonsReleased) != 0);
        }

        public int Trigger
        {
            get { return (int)PSMoveAPI.psmove_get_trigger(move); }
        }

        public float Temperature
        {
            get { return (float) PSMoveAPI.psmove_get_temperature_in_celsius(move); }
        }

        public PSMove_Battery_Level Battery
        {
            get { return (PSMove_Battery_Level)PSMoveAPI.psmove_get_battery(move); }
        }
        /*
        public int TriggerL2
        {
            get { return (int)PSMoveAPI.psmove_get_trigger(move); }
        }

        public int TriggerR2
        {
            get { return (int)PSMoveAPI.psmove_get_trigger(move); }
        }*/
        #endregion
        // ************
        // Extention Devises
        // **************

        int ext_connected = 0;
        bool wheelrum_on = false;
        //enum Extension_Device ext_device = 0x0; // unknown


        //public int TriggerL2()
        //{ TriggerVal();
        /*
        int l2 = 0, r2 = 0, c1 = 0, c2 = 0, throttle = 0;
        //IntPtr l2, r2, c1, c2, throttle;
        //l2 = 0;
      PSMoveAPI.psmove_get_ext_wheel(move, ref l2, ref r2, ref c1, ref c2, ref throttle);
        //PSMoveAPI.psmove_get_ext_wheel(move, l2, r2, c1, c2, throttle);
        if (l2 != 0 || r2 != 0 || c1 != 0 || c2 != 0 || throttle != 0)
            Console.WriteLine("L2: " + l2 + " R2: " + r2 + " c1: " + c1 + " c2: " + c2 + " throttle: " + throttle);
            */
        //  return (int)0;
        //}

        //private void TriggerVal()


        private void UpdateExt()
        {
            //this.exttype = PSMoveAPI.psmove_get_ext_type(move);
            //if (PSMoveAPI.psmove_is_ext_connected(move))
            //if (this.exttype !=0)
            if (ext_connected == 0 )
            {
                /* if the extension device was not connected before, report connect */
                //if (ext_connected == 0)
                if (PSMoveAPI.psmove_is_ext_connected(move))
                {
                    //Console.WriteLine("device connected");
                    this.exttype = PSMoveAPI.psmove_get_ext_type(move);
                    ext_connected = this.exttype;
                    switch (this.exttype)
                    {
                        case (1):
                            Console.WriteLine("Sharp shooter connected");
                            break;
                        case (2):
                            Console.WriteLine("Racing wheel connected");
                            break;
                        case (3):
                            Console.WriteLine("Unknown connected");
                            break;
                        default:
                            Console.WriteLine("Fail");
                            break;
                    }
                }
                //ext_connected = PSMoveAPI.psmove_get_ext_type(move);
                //this.exttype = (ushort)ext_connected;
                //ext_connected = 1;
                //ext_connected = this.exttype;
            }
            else
            {
                if (!PSMoveAPI.psmove_is_ext_connected(move))
                {
                    Console.WriteLine("Extension device disconnected!");
                    ext_connected = 0;
                    this.exttype =  0;
                }
                /* if the extension device was connected before, report disconnect *
                if (ext_connected != 0)
                {
                    Console.WriteLine("Extension device disconnected!");
                }
                ext_connected = 0;
                */
            }
        }
        private void UpdateWheel()
        {
            //while (PSMoveAPI.psmove_poll(move) == 0)
            {
            }
            //Console.WriteLine("querying ext device");
            //if (PSMoveAPI.psmove_is_ext_connected(move)) {
            if (ext_connected == 2)
            {
                /* if the extension device was not connected before, report connect */
                /*    if (ext_connected == 0)
                    {
                        Console.WriteLine("device connected");
                    }
                    ext_connected = 1;  
                    */
                int l2 = 0, r2 = 0, c1 = 0, c2 = 0, throttle = 0;

                PSMoveAPI.psmove_get_ext_wheel(move, ref l2, ref r2, ref throttle, ref c1, ref c2);
                //PSMoveAPI.psmove_get_ext_wheel(move, l2, r2, c1, c2, throttle);
                extwheel.Update(l2, r2, throttle, c1==1, c2==1);
                if (l2 != 0 || r2 != 0 || c1 != 0 || c2 != 0 || throttle != 0)
                {
                    //Console.WriteLine("L2: " + l2 + " R2: " + r2 + " c1: " + c1 + " c2: " + c2 + " throttle: " + throttle);
                    //PSMoveAPI.psmove_set_ext_wheel(move, l2, r2);
                }//else { PSMoveAPI.psmove_set_ext_wheel(move, 0, 0); }
            }
            /*
            else {
                /* if the extension device was connected before, report disconnect *
                if (ext_connected == 1)
                {
                    Console.WriteLine("Extension device disconnected!\n");
                }
                ext_connected = 0;
            }*/
        }

        private void UpdateWheelRumble()
        {
            if (ext_connected == 2)
            {
                if (wheelrum.l > 0 || wheelrum.r > 0)
                {
                    wheelrum_on = true;
                    PSMoveAPI.psmove_set_ext_wheel(move, wheelrum.l, wheelrum.r);
                    //Console.WriteLine("L: " + wheelrum.l + ", R: " + wheelrum.r);
                }
                else
                {
                    if (wheelrum_on)
                    {
                        PSMoveAPI.psmove_set_ext_wheel(move, 0, 0);
                        //Console.WriteLine("L: " + 0 + ", R: " + 0);
                        wheelrum_on = false;
                    }
                }
            }
        }

        private void UpdateShooter()
        {
            //while (PSMoveAPI.psmove_poll(move) == 0)
            {
            }
            //Console.WriteLine("querying ext device");
            //if (PSMoveAPI.psmove_is_ext_connected(move))
            if (ext_connected == 1)
            {
                /* if the extension device was not connected before, report connect *
                if (ext_connected == 0)
                {
                    Console.WriteLine("device connected");
                }
                ext_connected = 1;*/
                int fire = 0, rl =0, weapon = 0;

                PSMoveAPI.psmove_get_ext_shooter(move, ref fire, ref rl, ref weapon);
                //PSMoveAPI.psmove_get_ext_wheel(move, l2, r2, c1, c2, throttle);
                extshooter.Update(fire==1, rl==1, weapon);
                if (fire != 0 || rl != 0)
                {
                    //Console.WriteLine("L2: " + l2 + " R2: " + r2 + " c1: " + c1 + " c2: " + c2 + " throttle: " + throttle);
                    //PSMoveAPI.psmove_set_ext_wheel(move, l2, r2);
                }//else { PSMoveAPI.psmove_set_ext_wheel(move, 0, 0); }
            }/*
            else
            {
                /* if the extension device was connected before, report disconnect *
                if (ext_connected == 1)
                {
                    Console.WriteLine("Extension device disconnected!\n");
                }
                ext_connected = 0;
            }*/
        }

        // Update Functions

        public void Update()
        {
            UpdatePosition();
            PollInternalData(); // Polls internal IMU and buttons data
            UpdateOrientation();
            UpdateButtons();
            UpdateRumbleAndLED();
            UpdateExt();
            UpdateShooter();
            UpdateWheel();
            UpdateWheelRumble();
        }

        private void UpdatePosition()
        {
            // Update positional tracking info for this move
            PSMoveAPI.psmove_tracker_update(tracker.TrackerHandle, move);

            // Retrieve positional tracking data
            PSMoveAPI.psmove_fusion_get_position(tracker.FusionHandle, move,
                ref x, ref y, ref z);
            rawPosition.Update(x, y, z);
        }

        private void PollInternalData()
        {
            // Poll data (IMU and buttons)
            while (PSMoveAPI.psmove_poll(move) != 0) ;
        }

        private void UpdateOrientation()
        {
            // Orientation data
            PSMoveAPI.psmove_get_orientation(move, ref w, ref x, ref y, ref z);
            rotation.Update(w, z, x, y, true); // This update without conjugation solves the yaw problem - changed to true for hydra emulation

            // Gyroscope data
            PSMoveAPI.psmove_get_gyroscope_frame(move,
                PSMove.PSMove_Frame.Frame_SecondHalf,
                ref x, ref y, ref z);
            gyroscope.Update(x, y, z);

            // Accelerometer data
            PSMoveAPI.psmove_get_accelerometer_frame(move,
                PSMove.PSMove_Frame.Frame_SecondHalf,
                ref x, ref y, ref z);
            accelerometer.Update(x, y, z);
        }

        private void UpdateButtons()
        {
            // Button Events
            buttons = PSMoveAPI.psmove_get_buttons(move);
            PSMoveAPI.psmove_get_button_events(move, ref buttonsPressed, ref buttonsReleased);
        }

        private void UpdateRumbleAndLED()
        {
            // Update led color or set the automatic tracking recommended color
            UpdateLedColor();

            // Send rumble and leds notification back to the controller
            PSMoveAPI.psmove_update_leds(move);
        }

        private void UpdateLedColor()
        {
            // Check the camera tracking color update
            if (AutoLedColor)
            {
                // If the color is automatically recommended by the camera, get it now
                PSMoveAPI.psmove_tracker_get_color(tracker.TrackerHandle, move, ref r, ref g, ref b);
                led.SetColor(r, g, b);
            }
            else
            {
                // If the color is manually set, let the tracker know
                PSMoveAPI.psmove_tracker_set_camera_color(tracker.TrackerHandle, move, led.r, led.g, led.b);
            }
            // Finally set the led color
            PSMoveAPI.psmove_set_leds(move, led.r, led.g, led.b);
        }
    }

}
