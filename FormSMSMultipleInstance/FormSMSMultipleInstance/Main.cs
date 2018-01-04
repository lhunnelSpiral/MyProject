﻿using GsmComm.GsmCommunication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FormSMSMultipleInstance
{
    public partial class Main : Form
    {
        public List<Device> DeviceList = new List<Device>();
        public static List<SmsModem> ConnectedDevices = new List<SmsModem>();
        public string RgxInt = "\\d+((.|,)\\d+)?";
        
        public Main()
        {
            InitializeComponent();
            UpdateListDeviceList();
        }

        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            //Windows Message
            if (m.Msg == 537)
                UpdateListDeviceList();

            base.WndProc(ref m);
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            UpdateListDeviceList();
        }

        private void UpdateListDeviceList()
        {
            try
            {
                //lstAvailableDevice.Items.Clear();
                DeviceList.Clear();

                var task = Task.Factory.StartNew(UpdateDeviceList);
                Task.WaitAll(task);

                RemoveNotExitingDevice();

                if (DeviceList.Count != 0)
                {
                    foreach (var device in DeviceList.Distinct().ToArray())
                    {
                        //var isConnected = false;
                        var portNumber = GetPortNumber(device.Port);
                        var selectedDevice = ConnectedDevices.FirstOrDefault(c => c.Modem.Port == portNumber);

                        if (selectedDevice == null || !selectedDevice.OGsmModem.IsConnected())
                        {
                            selectedDevice?.TryDisconnect();
                        }
                        else
                        {
                            var selected = DeviceList.FirstOrDefault(c => c.Port == device.Port);

                            if (selected == null) continue;
                            selected.Status = "Connected";
                            selected.DeviceNumber = selectedDevice.GetOwnNumber();
                            RefreshGridView();
                        }
                    }
                }

                grdDevices.DataSource = DeviceList;

                grdDevices.Columns[0].Width = 50;
                grdDevices.Columns[1].Width = 80;
                grdDevices.Columns[2].Width = 100;

                RefreshGridView();
                RefreshButton();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void RemoveNotExitingDevice()
        {
            var notExistDevice = ConnectedDevices.Where(c => !DeviceList.Exists(b => c.Modem.Port == GetPortNumber(b.Port)));

            foreach (var device in notExistDevice)
            {
                device.TryDisconnect();
            }

            ConnectedDevices.RemoveAll(c => !DeviceList.Exists(b => c.Modem.Port == GetPortNumber(b.Port)));
        }

        private void UpdateDeviceList()
        {
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
                //ERROR ON THIS IF SAME THREAD
                var deviceList = searcher.Get();
                foreach (var device in deviceList)
                {
                    try
                    {
                        var deviceName = device["Name"].ToString();

                        if (!deviceName.Contains("UART") && !deviceName.Contains("Application")) continue;

                        var i = deviceName.IndexOf("COM", StringComparison.Ordinal);
                        var arr = deviceName.ToCharArray();
                        var str = "COM" + arr[i + 3];
                        if (arr[i + 4] != ')')
                        {
                            str += arr[i + 4];
                        }

                        //UPDATE LIST OF MODEM
                        DeviceList.Add(new Device(str));
                    }
                    catch
                    {
                        //do noting
                    }
                }
            }
            catch 
            {
                //do noting
            }
        }

        private int GetPortNumber(string portName)
        {
            var rgx = new Regex(RgxInt, RegexOptions.Compiled);
            var deviceName = portName;
            var matches = rgx.Matches(deviceName)[0].Value;
            int.TryParse(matches, out int portNumber);

            return portNumber;
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow selected in grdDevices.SelectedRows)
            {
                Task.Factory.StartNew(() => ConnectDevice(selected.Index));
            }
        }

        private void RefreshGridView()
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)RefreshGridView);
                return;
            }
            lblDevicesCount.Text = DeviceList.Count.ToString();
            grdDevices.Enabled = DeviceList.Count != 0;
            grdDevices.Refresh();
        }
        
        private void ConnectDevice(int index)
        {
            var device = new SmsModem();

            try
            {
                DeviceList[index].Status = "Connecting...";
                RefreshGridView();
                var deviceName = grdDevices.Rows[index].Cells[0].Value.ToString();
                var portNumber = GetPortNumber(deviceName);
                var modem = new ModemConfig
                {
                    Port = portNumber,
                    BaudRate = GsmCommMain.DefaultBaudRate,
                    Timeout = GsmCommMain.DefaultTimeout
                };

                device = new SmsModem
                {
                    Modem = modem
                };
                device.ConnectModem();

                device.OGsmModem.MessageReceived += Comm_MessageReceived;
                //update Status
                ConnectedDevices.Add(device);

                DeviceList[index].Status = "Connected";
                DeviceList[index].DeviceNumber = device.GetOwnNumber();

                RefreshGridView();

            }
            catch (Exception error)
            {
                //MessageBox.Show(error.Message);
                DeviceList[index].Status = error.Message;
                RefreshGridView();
                device.TryDisconnect();
            }
            
            RefreshButton();
        }

        private static void Comm_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                var modemNewSms = ConnectedDevices.FirstOrDefault(c => c.Modem.Port == ((GsmCommMain)sender).PortNumber);

                if (modemNewSms == null) return;
                var messages = modemNewSms.ReadMessageUnread();

                foreach (var sms in messages)
                {
                    MessageBox.Show($@"Device {modemNewSms.Modem.Port} Has New Message From ({sms.Sender}), Contains: {sms.Message}",modemNewSms.GetOwnNumber());
                    modemNewSms.DeleteMessage(sms.Index);
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
            }
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow selected in grdDevices.SelectedRows)
            {
                var deviceName = grdDevices.Rows[selected.Index].Cells[0].Value.ToString();
                var portNumber = GetPortNumber(deviceName);

                var selectedDevice = ConnectedDevices.FirstOrDefault(c => c.Modem.Port == portNumber);

                if (!DisconnectDevice(selectedDevice)) continue;

                DeviceList[selected.Index].Status = "";
                grdDevices.Refresh();
                RefreshButton();
            }

        }

        private static bool DisconnectDevice(SmsModem device)
        {
            var isDisconnected = false;
            
            device.TryDisconnect();
            ConnectedDevices.Remove(device);

            device.OGsmModem.MessageReceived -= Comm_MessageReceived;

            if (!device.OGsmModem.IsConnected())
            {
                isDisconnected = true;
            }

            return isDisconnected;
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (var device in ConnectedDevices)
            {
                device.TryDisconnect();
            }
        }
        
        private void BtnUpdateNumber_Click(object sender, EventArgs e)
        {

            Task.Factory.StartNew(UpdateContacNumber);
        }

        private void UpdateContacNumber()
        {
            var newContact = string.Empty;
            var index = grdDevices.SelectedRows[0].Index;
            var deviceName = grdDevices.SelectedRows[0].Cells[0].Value.ToString();
            var portNumber = GetPortNumber(deviceName);

            var selectedDevice = ConnectedDevices.FirstOrDefault(c => c.Modem.Port == portNumber);
            InputBox.ShowInputDialog(ref newContact);

            if (selectedDevice != null)
            {
                selectedDevice.SetOwnNumber(newContact);

                selectedDevice.UpdateOwnContact(newContact);

                DeviceList[index].DeviceNumber = selectedDevice.GetOwnNumber();
            }

            RefreshGridView();

        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow selected in grdDevices.SelectedRows)
            {
                Task.Factory.StartNew(() => UpdateDevice(selected.Index));
            }
        }

        private void BtnUpdateAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow selected in grdDevices.Rows)
            {
                Task.Factory.StartNew(() => UpdateDevice(selected.Index));
            }
        }

        private void UpdateDevice(int index)
        {
            try
            {
                if (DeviceList[index].Status == "Connected") return;

                var deviceName = grdDevices.Rows[index].Cells[0].Value.ToString();
                DeviceList[index].Status = "Checking...";
                RefreshGridView();

                var portNumber = GetPortNumber(deviceName);
                var modem = new ModemConfig
                {
                    Port = portNumber,
                    BaudRate = GsmCommMain.DefaultBaudRate,
                    Timeout = GsmCommMain.DefaultTimeout
                };

                var device = new SmsModem
                {
                    Modem = modem
                };

                if (device.TryConnectModem())
                {
                    DeviceList[index].Status = "Ready to Connect";
                    RefreshGridView();
                }
                else
                {
                    DeviceList[index].Status = "N/A";
                    RefreshGridView();
                }
            }
            catch (Exception ex)
            {
                DeviceList[index].Status = ex.Message;
                RefreshGridView();
            }
        }

        private void grdDevices_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void grdDevices_SelectionChanged(object sender, EventArgs e)
        {

            RefreshButton();
        }

        private void RefreshButton()
        {

            if (InvokeRequired)
            {
                Invoke((MethodInvoker)RefreshButton);
                return;
            }
            
            try
            {
                //var selectedRow = (DataGridView)sender;

                var deviceName = grdDevices.SelectedRows[0].Cells[0].Value.ToString();
                var portNumber = GetPortNumber(deviceName);

                var selectedDevice = ConnectedDevices.FirstOrDefault(c => c.Modem.Port == portNumber);
                if (selectedDevice != null)
                {
                    btnConnect.Enabled = !selectedDevice.OGsmModem.IsConnected();
                    btnUpdateNumber.Enabled = selectedDevice.OGsmModem.IsConnected();
                    btnDisconnect.Enabled = selectedDevice.OGsmModem.IsConnected();
                }
                else
                {
                    btnConnect.Enabled = true;
                    btnUpdateNumber.Enabled = false;
                    btnDisconnect.Enabled = false;
                }
            }
            catch
            {
                //do nothing
            }
        }

        private void grdDevices_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {

        }
    }
}