using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

namespace bill_to_bill
{
    public partial class Form1 : Form
    {
        const string SERIAL_PORT = "COM2";
        Lucker.Devices.BillToBillDevice m_Dev;


        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string devicePortName;
            var deviceFounded = 
                Lucker.Devices.BillToBillDevice.SearchDevice(out devicePortName);
            if (deviceFounded)
            {
                Debug.Assert(devicePortName != null, "devicePortName != null");
                var device = new Lucker.Devices.BillToBillDevice(SERIAL_PORT);
                device.OnPollStatus +=
                    delegate(object s,
                    Lucker.Devices.BillToBillDevice.PollStatus pollStatus)
                    {
                        Debug.WriteLine(
                            "OnPollStatus : " + pollStatus.PollResponseCode);
                        switch (pollStatus.PollResponseCode)
                        {
                            case 
                            Lucker.Devices.BillToBillDevice.PollResponseCode.Unit_Disabled:
                                this.Invoke(new MethodInvoker(
                                    delegate
                                    {
                                        //enable btn
                                        button2.Enabled = true;
                                        //disable btn
                                        button3.Enabled = false;
                                        //disp btn
                                        button4.Enabled = true;
                                        //get cassette status
                                        button6.Enabled = true;
                                        //set cassette status
                                        button7.Enabled = true;

                                    }));
                                break;
                            case Lucker.Devices.BillToBillDevice.PollResponseCode.Idle:
                                this.Invoke(new MethodInvoker(
                                    delegate
                                    {
                                        //enable btn
                                        button2.Enabled = false;
                                        //disable btn
                                        button3.Enabled = true;
                                        //disp btn
                                        button4.Enabled = true;
                                        //get cassette status
                                        button6.Enabled = true;
                                        //set cassette status
                                        button7.Enabled = true;
                                    }));
                                break;
                            default:
                                this.Invoke(new MethodInvoker(
                                delegate
                                {
                                    //enable btn
                                    button2.Enabled = false;
                                    //disable btn
                                    button3.Enabled = false;
                                    //disp btn
                                    button4.Enabled = false;
                                    //get cassette status
                                    button6.Enabled = false;
                                    //set cassette status
                                    button7.Enabled = false;
                                }));
                                break;

                        }
          
                    };
                device.OnBillIncome +=
                    delegate(object s,
                    Lucker.Devices.BillToBillDevice.BillIncomeEventArgs e1)
                    {
                        Debug.WriteLine("OnBillIncome " + e1.BillNominal);
                    };
                device.OnDispensing +=
                    delegate(object s,EventArgs e1)
                    {
                        Debug.WriteLine("On Dispensing");
                    };
                device.OnDispenseEnd +=
                    delegate(object s, EventArgs e1)
                    {
                        Debug.WriteLine("On Dispense End");
                    };
                
                device.LoadSettingsAndStart();
                
                m_Dev = device;

                button1.Enabled = false;
                
                /*button2.Enabled = button3.Enabled = button5.Enabled =
                    button6.Enabled = button7.Enabled = true;*/

                comboBox1.DataSource = m_Dev.ResyclingCassettes;
                comboBox1.DisplayMember = "CassetteNumber";
                comboBox1.ValueMember = "CassetteNumber";

                comboBox2.DataSource = m_Dev.BillTypes;
                
                //var billTypes = m_Dev.GetBillTypes();
                /*foreach (var billType in billTypes)
                {
                    comboBox1.Items.Add(billType);
                    comboBox2.Items.Add(billType);
                }*/
            }
            /*var b2b = new Lucker.Devices.BillToBillDevice.;
            b2b.LoadSettingsAndStart();
            b2b.Test();*/

            
        }



        private void button2_Click(object sender, EventArgs e)
        {

            m_Dev.ManualEnable();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            m_Dev.ManualDisable();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            m_Dev.Dispense((int)numericUpDown1.Value);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            m_Dev.GetStatus();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //loop via all available cassettes
            foreach (var cassette in m_Dev.ResyclingCassettes)
            {
                Lucker.Devices.BillToBillDevice.BillType billType;
                //get cassette bill type assigned
                var getBillTypeOk = cassette.GetBillType(out billType);
                Debug.Assert(getBillTypeOk != false,"getBillTypeOk != false");
                //get cassette bill count
                int cassetteNumberOfBills;
                var getNumberOfBillsOk = cassette.GetNumberOfBills(
                    out cassetteNumberOfBills);
                Debug.Assert(getNumberOfBillsOk == true,
                    "getNumberOfBillsOk == true");
                //get cassette is full status
                bool cassetteIsFull;
                var getCassetteIsFull = cassette.IsFull(out cassetteIsFull);
                Debug.Assert(getCassetteIsFull == true,"getCassetteIsFull == true");


                Debug.WriteLine(
                    string.Format("nominal - {0}; bill count - {1} ; is full - {2}", 
                    billType.Nominal(),cassetteNumberOfBills,cassetteIsFull));
            }
            
        }

        private void button7_Click(object sender, EventArgs e)
        {
            
            var billTypeSetOk = m_Dev.ResyclingCassettes[
                (byte)comboBox1.SelectedValue].SetBillType(
                (Lucker.Devices.BillToBillDevice.BillType)comboBox2.SelectedItem);
            Debug.Assert(billTypeSetOk == true, "billTypeSetOk == true");

            
            

            /*var billTypeSetOk1 = m_Dev.ResyclingCassettes[1].SetBillType(
                (Lucker.Devices.BillToBillDevice.BillType)comboBox2.SelectedItem);
            Debug.Assert(billTypeSetOk1 == true, "billTypeSetOk1 == true");*/


        }



    }
}
