﻿using System;
using System.Collections.Generic;
using HA4IoT.Contracts.Areas;
using HA4IoT.Contracts.Sensors;
using HA4IoT.Contracts.Services;
using System.Reactive.Linq;
using System.Linq;
using HA4IoT.Extensions.MotionModel;
using HA4IoT.Contracts.Components.Features;
using HA4IoT.Contracts.Services.System;
using HA4IoT.Contracts.Services.Daylight;

namespace HA4IoT.Extensions
{
    public class LightAutomationService : IService, IDisposable
    {
        private const int MOTION_TIME_WINDOW = 3000;

        private readonly IAreaRegistryService _areaService;
        private readonly ISchedulerService _schedulerService;
        private readonly IDaylightService _daylightService;
        
        private readonly List<IDisposable> _resources = new List<IDisposable>();
        private readonly Dictionary<IMotionDetector, MotionDescriptor> _motionDescriptors = new Dictionary<IMotionDetector, MotionDescriptor>();

        private bool _hasStarted = false;

         public LightAutomationService(IAreaRegistryService areaService, 
                                      ISchedulerService schedulerService, 
                                      IDaylightService daylightService
        )
        {
            _areaService = areaService;
            _schedulerService = schedulerService;
            _daylightService = daylightService;
        }

        public void Startup()
        {
            _hasStarted = true;

            _motionDescriptors.Values.ToList().ForEach(x => x.InitDescriptor());
        }
        
        public MotionDescriptor RegisterMotionDescriptor(MotionDescriptor descriptor)
        {
            if(descriptor == null) new ArgumentNullException(nameof(descriptor));

            if(_hasStarted)
            {
                throw new Exception("Cannot register new descriptors after service has started");
            }
            
            descriptor.SetArea(GetAreaForDetector(descriptor.MotionDetector));
            

            if (descriptor.Lamp?.GetFeatures()?.Supports<PowerStateFeature>() ?? false)
            {
                throw new Exception($"Component {descriptor?.Lamp?.Id} is not supporting PowerStateFeature");
            }

            return descriptor;
        }

        private IArea GetAreaForDetector(IMotionDetector md)
        {
            foreach (var area in _areaService.GetAreas())
            {
                if(area.GetComponent<IMotionDetector>(md.Id) != null)
                {
                    return area;
                }
            }

            return null;
        }

        public void StartWatchForMove()
        {
            var detectors = _motionDescriptors.Values
                                              .Select(x => x.MotionSource)
                                              .Merge()
                                              .Select(messages => messages);

            
            var motion = detectors.Timestamp()
                                  .Do(y =>
                                  {
                                      var descriptor = _motionDescriptors[y.Value];
                                      descriptor.SetLastMotionTime(y.Timestamp);
                                      descriptor.TurnOnLamp();
                                      
                                  })
                                  .Buffer(detectors, (x) => { return Observable.Timer(TimeSpan.FromMilliseconds(MOTION_TIME_WINDOW)); })
                                  .Select(x =>
                                  {
                                        var vector = new MotionVector();

                                        //foreach (var ev in x)
                                        //{
                                        //    if (vector.Path.Count == 0)
                                        //    {
                                        //        vector.Path.Add(new MotionPoint(ev.Value.MotionDetector, ev.Timestamp));
                                        //        continue;
                                        //    }

                                        //    if (ev.Value.MotionDetector == _motionDetectors[vector.End.MotionDetector].Neighbor)
                                        //    {
                                        //        vector.Path.Add(new MotionPoint(ev.Value.MotionDetector, ev.Timestamp));
                                        //    }
                                        //}

                                        return vector;
                                  })
                                  .Where(y => y.Path.Count > 1)
                                  .DistinctUntilChanged();

          
            var resource = motion.Subscribe(x =>
            {
                var time = DateTime.Now;
               Console.WriteLine($"[{time.Minute}:{time.Second}:{time.Millisecond}] {x.ToString()}");
                
            });

            AddResourceToDisposeList(resource);
        }
    
        private void AddResourceToDisposeList(IDisposable resource)
        {
            _resources.Add(resource);
        }

        public void Dispose()
        {
            foreach(var resource in _resources)
            {
                resource.Dispose();
            }
        }
    }
}