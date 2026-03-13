using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class MeasurementAttribute : PropertyAttribute
{
    public MeasurementUnit Measurement { get; }
    
    public MeasurementAttribute(MeasurementUnit measurement)
    {
        Measurement = measurement;
    }
}