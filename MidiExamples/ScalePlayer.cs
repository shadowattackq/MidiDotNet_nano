﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Midi;

namespace MidiExamples
{
    public class ScalePlayer : ExampleBase
    {
        public ScalePlayer()
            : base("ScalePlayer.cs", "Plays a scale from each MIDI key pressed.")
        { }
        
        class Drummer
        {
            public Drummer(MidiClock clock, MidiOutputDevice outputDevice, int beatsPerMeasure)
            {
                this.clock = clock;
                this.outputDevice = outputDevice;
                this.beatsPerMeasure = beatsPerMeasure;
                this.messagesForOneMeasure = new List<MidiMessage>();
                for (int i = 0; i < beatsPerMeasure; ++i) {
                    int note = i == 0 ? 44 : 42;
                    int velocity = i == 0 ? 100 : 40;
                    messagesForOneMeasure.Add(new NoteOnOffMessage(outputDevice, 9, note, velocity, i, 0.99f));
                }
                messagesForOneMeasure.Add(new CallbackMessage(new CallbackMessage.CallbackType(CallbackHandler), 0));
                clock.Schedule(messagesForOneMeasure, 0);
            }
            private MidiMessage[] CallbackHandler(float beatTime)
            {
                // Round up to the next measure boundary.
                float timeOfNextMeasure = beatTime + beatsPerMeasure;
                clock.Schedule(messagesForOneMeasure, timeOfNextMeasure);
                return null;
            }
            private MidiClock clock;
            private MidiOutputDevice outputDevice;
            private int beatsPerMeasure;
            private List<MidiMessage> messagesForOneMeasure;
        }

        class Scaler
        {
            public Scaler(MidiClock clock, MidiInputDevice inputDevice, MidiOutputDevice outputDevice)
            {
                this.clock = clock;
                this.inputDevice = inputDevice;
                this.outputDevice = outputDevice;
                if (inputDevice != null)
                {
                    inputDevice.NoteOn += new MidiInputDevice.NoteOnHandler(this.NoteOn);
                }
            }

            public void NoteOn(NoteOnMessage msg)
            {
                int[] scale = NoteUtil.MajorScaleStartingAt(msg.Note);
                for (int i = 1; i < scale.Count(); ++i)
                {
                    clock.Schedule(new NoteOnOffMessage(outputDevice, msg.Channel, scale[i],
                    msg.Velocity, msg.BeatTime + i, 0.99f));
                }
            }

            private MidiClock clock;
            private MidiInputDevice inputDevice;
            private MidiOutputDevice outputDevice;
        }

        public override void Run()
        {            
            if (MidiOutputDevice.InstalledDevices.Count == 0)
            {
                Console.WriteLine("Can't do anything with no output device.");
                return;
            }

            float beatsPerMinute = 180;
            MidiClock clock = new MidiClock(beatsPerMinute);

            MidiOutputDevice outputDevice = MidiOutputDevice.InstalledDevices[0];
            outputDevice.Open();

            Drummer drummer = new Drummer(clock, outputDevice, 4);

            MidiInputDevice inputDevice = null;
            if (MidiInputDevice.InstalledDevices.Count > 0)
            {
                // Just pick the first input device.  This will throw an exception if there isn't one.
                inputDevice = MidiInputDevice.InstalledDevices[0];
                inputDevice.Open(() => clock.BeatTime);
            }
            Scaler scaler = new Scaler(clock, inputDevice, outputDevice);

            clock.Start();
            if (inputDevice != null)
            {
                inputDevice.StartReceiving();
            }

            bool done = false;

            while (!done)
            {
                Console.Clear();
                Console.WriteLine("BPM = {0}, Playing = {1}", clock.BeatsPerMinute, clock.IsRunning);
                Console.WriteLine("'Q' = Quit, '[' = slower, ']' = faster, 'P' = Toggle Play");
                ConsoleKey key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Q)
                {
                    done = true;
                }
                else if (key == ConsoleKey.Oem4)
                {
                    clock.BeatsPerMinute -= 2;
                }
                else if (key == ConsoleKey.Oem6)
                {
                    clock.BeatsPerMinute += 2;
                }
                else if (key == ConsoleKey.P)
                {
                    if (clock.IsRunning)
                    {
                        clock.Stop();
                        if (inputDevice != null)
                        {
                            inputDevice.StopReceiving();
                        }
                        outputDevice.SilenceAllNotes();
                    }
                    else
                    {
                        clock.Start();
                        if (inputDevice != null)
                        {
                            inputDevice.StartReceiving();
                        }
                    }
                }
                else if (key == ConsoleKey.D1)
                {
                    NoteOnMessage msg = new NoteOnMessage(outputDevice, 0, 60, 80, clock.BeatTime);
                    NoteOffMessage msg2 = new NoteOffMessage(outputDevice, 0, 60, 80, clock.BeatTime+0.99f);
                    clock.Schedule(msg);
                    clock.Schedule(msg2);
                    scaler.NoteOn(msg);
                }
            }

            if (clock.IsRunning)
            {
                clock.Stop();
                if (inputDevice != null)
                {
                    inputDevice.StopReceiving();
                }
                outputDevice.SilenceAllNotes();
            }

            outputDevice.Close();
            if (inputDevice != null)
            {
                inputDevice.Close();
            }
        }
    }
}