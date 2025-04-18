﻿using Meadow.Hardware;
using System;
using System.Threading.Tasks;

namespace Meadow.Foundation.ICs.CAN;

public partial class Mcp2515
{
    /// <summary>
    /// Represents a CAN bus using the MCP2515
    /// </summary>
    public class Mcp2515CanBus : ICanBus
    {
        private int _currentMask = 0;

        /// <inheritdoc/>
        public event EventHandler<ICanFrame>? FrameReceived;
        /// <inheritdoc/>
        public event EventHandler<CanErrorInfo>? BusError;

        private Mcp2515 Controller { get; }

        /// <inheritdoc/>
        public CanBitrate BitRate
        {
            get => Controller.bitrate;
            set => Controller.Initialize(value, Controller.oscillator);
        }

        /// <inheritdoc/>
        public CanAcceptanceFilterCollection AcceptanceFilters { get; } = new(5);

        internal Mcp2515CanBus(Mcp2515 controller)
        {
            Controller = controller;

            if (Controller.InterruptPort != null)
            {
                Controller.InterruptPort.Changed += OnInterruptPortChanged;
            }

            AcceptanceFilters.CollectionChanged += OnAcceptanceFiltersChanged;
        }

        private void OnAcceptanceFiltersChanged(object? sender, (System.ComponentModel.CollectionChangeAction Action, CanAcceptanceFilter Filter) e)
        {
            switch (e.Action)
            {
                case System.ComponentModel.CollectionChangeAction.Add:
                    if (e.Filter is CanStandardExactAcceptanceFilter sefa)
                    {
                        var newMask = 0x7ff;

                        Controller.SetMaskAndFilter(false, newMask, sefa.AcceptID, AcceptanceFilters.Count - 1);

                        _currentMask = newMask;
                    }
                    else if (e.Filter is CanExtendedExactAcceptanceFilter eef)
                    {
                        var newMask = _currentMask | eef.AcceptID;

                        Controller.SetMaskAndFilter(true, newMask, eef.AcceptID, AcceptanceFilters.Count - 1);

                        _currentMask = newMask;
                    }
                    else if (e.Filter is CanStandardRangeAcceptanceFilter srf)
                    {
                    }
                    else if (e.Filter is CanExtendedRangeAcceptanceFilter erf)
                    {
                    }

                    break;
                case System.ComponentModel.CollectionChangeAction.Remove:
                    if (e.Filter is CanStandardExactAcceptanceFilter sefr)
                    {
                        var newMask = 0x00;

                        _currentMask = newMask;
                    }
                    else if (e.Filter is CanExtendedExactAcceptanceFilter eef)
                    {
                    }
                    else if (e.Filter is CanStandardRangeAcceptanceFilter srf)
                    {
                    }
                    else if (e.Filter is CanExtendedRangeAcceptanceFilter erf)
                    {
                    }

                    break;

            }
        }

        private void OnInterruptPortChanged(object sender, DigitalPortResult e)
        {
            // TODO: check why the interrupt happened (error, frame received, etc)
            var canstat = (InterruptCode)Controller.ReadRegister(Register.CANSTAT)[0] & InterruptCode.Mask;

            switch (canstat)
            {
                case InterruptCode.RXB0:
                case InterruptCode.RXB1:
                    if (FrameReceived != null)
                    {
                        var frame = ReadFrame();
                        Task.Run(() => FrameReceived.Invoke(this, frame));
                    }
                    break;
                case InterruptCode.Error:
                    if (BusError != null)
                    {
                        var errors = Controller.ReadRegister(Register.EFLG)[0];
                        // read the error counts
                        var tec = Controller.ReadRegister(Register.TEC)[0];
                        var rec = Controller.ReadRegister(Register.REC)[0];
                        BusError.Invoke(this, new CanErrorInfo
                        {
                            ReceiveErrorCount = rec,
                            TransmitErrorCount = tec
                        });
                        // clear the error interrupt
                        Controller.ClearInterrupt(InterruptFlag.ERRIF | InterruptFlag.MERRF);
                    }
                    break;
            }
        }

        /// <inheritdoc/>
        public bool IsFrameAvailable()
        {
            var status = Controller.GetStatus();

            if ((status & Status.RX0IF) == Status.RX0IF)
            {
                return true;
            }
            else if ((status & Status.RX1IF) == Status.RX1IF)
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void WriteFrame(ICanFrame frame)
        {
            Controller.WriteFrame(frame, 0);
        }

        /// <inheritdoc/>
        public ICanFrame? ReadFrame()
        {
            var status = Controller.GetStatus();

            if ((status & Status.RX0IF) == Status.RX0IF)
            { // message in buffer 0
                return Controller.ReadDataFrame(RxBufferNumber.RXB0);
            }
            else if ((status & Status.RX1IF) == Status.RX1IF)
            { // message in buffer 1
                return Controller.ReadDataFrame(RxBufferNumber.RXB1);
            }
            else
            { // no messages available
                return null;
            }
        }

        /// <inheritdoc/>
        public void ClearReceiveBuffers()
        {
            var status = Controller.GetStatus();

            if ((status & Status.RX0IF) == Status.RX0IF)
            { // message in buffer 0
                Controller.ReadDataFrame(RxBufferNumber.RXB0);
            }

            if ((status & Status.RX1IF) == Status.RX1IF)
            { // message in buffer 1
                Controller.ReadDataFrame(RxBufferNumber.RXB1);
            }

            // clear erase rx interrupts
            Controller.ClearInterrupt(InterruptFlag.RX0IF | InterruptFlag.RX1IF);
        }
    }
}