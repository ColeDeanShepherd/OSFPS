using System.Collections.Generic;
using UnityEngine.Networking;

namespace NetworkLibrary
{
    public class BandwidthMovingAverager
    {
        public float OutgoingBandwidthInBytes
        {
            get
            {
                if (samples.Count < 2) return 0;

                var oldestSample = samples[0];
                var newestSample = samples[samples.Count - 1];
                var outgoingBytesDuringWindow = newestSample.SentByteCount - oldestSample.SentByteCount;
                var outgoingBandwidthDuringWindow = (float)outgoingBytesDuringWindow / (newestSample.Time - oldestSample.Time);

                return outgoingBandwidthDuringWindow;
            }
        }

        public BandwidthMovingAverager()
        {
            windowDuration = 1;
            samples = new List<BandwidthSample>();
        }
        public void Update()
        {
            samples.Add(GetCurrentSample());
            RemoveOldSamples();
        }

        private struct BandwidthSample
        {
            public float Time;
            public int SentByteCount;
        }
        private float windowDuration;
        private List<BandwidthSample> samples;

        private BandwidthSample GetCurrentSample()
        {
            return new BandwidthSample
            {
                Time = UnityEngine.Time.realtimeSinceStartup,
                SentByteCount = NetworkTransport.GetOutgoingFullBytesCount()
            };
        }
        private void RemoveOldSamples()
        {
            var currentTime = UnityEngine.Time.realtimeSinceStartup;
            var indexOfLastSampleToRemove = samples.FindLastIndex(bs => (currentTime - bs.Time) > windowDuration);

            if (indexOfLastSampleToRemove >= 0)
            {
                var numberOfSamplesToRemove = 1 + indexOfLastSampleToRemove;
                samples.RemoveRange(0, numberOfSamplesToRemove);
            }
        }
    }
}