﻿using System;
using Unity.Collections;
using Unity.Jobs;

namespace UnityEngine.Experimental.Perception.Randomization.Samplers
{
    /// <summary>
    /// Returns a constant value when sampled
    /// </summary>
    [Serializable]
    public struct ConstantSampler : ISampler
    {
        /// <summary>
        /// The value from which samples will be generated
        /// </summary>
        public float value;

        /// <summary>
        /// The base seed used to initialize this sampler's state.
        /// Note that ConstantSamplers do not utilize a baseSeed.
        /// </summary>
        public uint baseSeed
        {
            get => SamplerUtility.largePrime;
            set { }
        }

        /// <summary>
        /// The current random state of this sampler.
        /// Note that ConstantSamplers do not utilize a random state.
        /// </summary>
        public uint state
        {
            get => SamplerUtility.largePrime;
            set { }
        }

        /// <summary>
        /// A range bounding the values generated by this sampler
        /// </summary>
        public FloatRange range
        {
            get => new FloatRange(value, value);
            set { }
        }

        /// <summary>
        /// Constructs a new ConstantSampler
        /// </summary>
        /// <param name="value">The value from which samples will be generated</param>
        public ConstantSampler(float value)
        {
            this.value = value;
        }

        /// <summary>
        /// Resets a sampler's state to its base random seed.
        /// Note that there is no state to reset for ConstantSamplers.
        /// </summary>
        public void ResetState() { }

        /// <summary>
        /// Deterministically offsets a sampler's state.
        /// Note that ConstantSamplers do not have a state to iterate.
        /// </summary>
        /// <param name="offsetIndex">
        /// The index used to offset the sampler's state.
        /// Typically set to either the current scenario iteration or a job's batch index.
        /// </param>
        public void IterateState(int offsetIndex) { }

        /// <summary>
        /// Generates one sample
        /// </summary>
        /// <returns>The generated sample</returns>
        public float Sample()
        {
            return value;
        }

        /// <summary>
        /// Schedules a job to generate an array of samples
        /// </summary>
        /// <param name="sampleCount">The number of samples to generate</param>
        /// <param name="jobHandle">The handle of the scheduled job</param>
        /// <returns>A NativeArray of generated samples</returns>
        public NativeArray<float> Samples(int sampleCount, out JobHandle jobHandle)
        {
            return SamplerUtility.GenerateSamples(this, sampleCount, out jobHandle);
        }
    }
}
