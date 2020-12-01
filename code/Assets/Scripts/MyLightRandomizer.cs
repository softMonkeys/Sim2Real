using System;
using UnityEngine;
using UnityEngine.Experimental.Perception.Randomization.Parameters;
using UnityEngine.Experimental.Perception.Randomization.Randomizers;

[Serializable]
[AddRandomizerMenu("Perception/My Light Randomizer")]
public class MyLightRandomizer : Randomizer
{
	public FloatParameter lightIntensityParameter;
	public ColorRgbParameter lightColorParameter;

	protected override void OnIterationStart()
	{
		var taggedObjects = tagManager.Query<MyLightRandomizerTag>();
		foreach (var taggedObject in taggedObjects)
		{
			var light = taggedObject.GetComponent<Light>();
			light.color = lightColorParameter.Sample();

			var tag = taggedObject.GetComponent<MyLightRandomizerTag>();
			tag.SetIntensity(lightIntensityParameter.Sample());
		}
	}
}