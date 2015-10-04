﻿using System;

namespace CK.HomeAutomation.Hardware.RemoteSwitch.Codes
{
    public class BrennenstuhlCodeSequenceProvider
    {
        [Flags]
        public enum SystemCode
        {
            AllOff = 0, // Used to affect the state where all DIP switches are off.
            Switch1 = 1,
            Switch2 = 2,
            Switch3 = 4,
            Switch4 = 8,
            Switch5 = 16,
            AllOn = Switch1 | Switch2 | Switch3 | Switch4 | Switch5
        }

        public enum UnitCode
        {
            A,
            B,
            C,
            D
            //E -- is not used (taken from official documentation)
        }

        public LPD433MhzCodeSequence GetSequence(SystemCode systemCode, UnitCode unitCode, RemoteSwitchCommand command)
        {
            // Examples:
            // System Code = 11111
            //00000000|00000000000|0010101|010001 = 1361 A ON
            //00000000|00000000000|0010101|010100 = 1364 A OFF
            //00000000|00000000000|1000101|010001 = 4433 B ON
            //00000000|00000000000|1000101|010100 = 4436 B OFF
            //00000000|00000000000|1010001|010001 = 5201 C ON
            //00000000|00000000000|1010001|010100 = 5204 C OFF
            //00000000|00000000000|1010100|010001 = 5393 D ON
            //00000000|00000000000|1010100|010100 = 5396 D OFF
            // System Code = 00000
            //00000000|01010101010|0010101|010001 = 5588305 A ON
            //00000000|01010101010|0010101|010100 = 5588308 A OFF
            //00000000|01010101010|1000101|010001 = 5591377 B ON
            //00000000|01010101010|1000101|010100 = 5591380 B OFF
            //00000000|01010101010|1010001|010001 = 5592145 C ON
            //00000000|01010101010|1010001|010100 = 5592148 C OFF
            //00000000|01010101010|1010100|010001 = 5592337 D ON
            //00000000|01010101010|1010100|010100 = 5592340 D OFF
            // System Code = 10101
            //00000000|00010001000|0010101|010001 = 1115473 A ON
            //00000000|00010001000|0010101|010100 = 1115476 A OFF
            //00000000|00010001000|1000101|010001 = 1118545 B ON
            //00000000|00010001000|1000101|010100 = 1118548 B OFF
            //00000000|00010001000|1010001|010001 = 1119313 C ON
            //00000000|00010001000|1010001|010100 = 1119316 C OFF
            //00000000|00010001000|1010100|010001 = 1119505 D ON
            //00000000|00010001000|1010100|010100 = 1119508 D OFF

            ulong code = 0UL;
            code = SetSystemCode(code, systemCode);
            code = SetUnitCode(code, unitCode);
            code = SetCommand(code, command);

            return new LPD433MhzCodeSequence().WithCode(new LPD433MhzCode(code, 24));
        }

        private ulong SetSystemCode(ulong code, SystemCode systemCode)
        {
            // A LOW switch is binary 10 and a HIGH switch is binary 00.
            // The values of the DIP switches are inverted.
            if (!systemCode.HasFlag(SystemCode.Switch1))
            {
                code |= 1UL << 22;
            }

            if (!systemCode.HasFlag(SystemCode.Switch2))
            {
                code |= 1UL << 20;
            }

            if (!systemCode.HasFlag(SystemCode.Switch3))
            {
                code |= 1UL << 18;
            }

            if (!systemCode.HasFlag(SystemCode.Switch4))
            {
                code |= 1UL << 16;
            }

            if (!systemCode.HasFlag(SystemCode.Switch5))
            {
                code |= 1UL << 14;
            }

            return code;
        }

        private ulong SetUnitCode(ulong code, UnitCode unitCode)
        {
            ulong unitCodeValue;

            switch (unitCode)
            {
                case UnitCode.A:
                    {
                        unitCodeValue = 0x15;
                        break;
                    }

                case UnitCode.B:
                    {
                        unitCodeValue = 0x45;
                        break;
                    }

                case UnitCode.C:
                    {
                        unitCodeValue = 0x51;
                        break;
                    }

                case UnitCode.D:
                    {
                        unitCodeValue = 0x54;
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            code |= unitCodeValue << 6;
            return code;
        }

        private ulong SetCommand(ulong code, RemoteSwitchCommand command)
        {
            switch (command)
            {
                case RemoteSwitchCommand.TurnOn:
                    {
                        code |= 0x11;
                        break;
                    }

                case RemoteSwitchCommand.TurnOff:
                    {
                        code |= 0x14;
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            return code;
        }
    }
}
