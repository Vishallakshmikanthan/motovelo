using UnityEngine;

namespace Motovelo
{
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralV6Audio : MonoBehaviour
    {
        public bool audioEnabled = true;
        public bool engineActive;
        public float rpm = 5000f;
        public float throttle;
        public float slip;
        public float kerb;

        private double _phase1;
        private double _phase2;
        private double _phaseTurbo;
        private double _sampleRate = 44100.0;
        private float _shiftPopEnv;
        private float _beepEnv;
        private double _beepFreq = 880.0;
        private uint _rng = 1234567u;

        private void Awake()
        {
            try
            {
                _sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100.0;
                var src = GetComponent<AudioSource>();
                if (src.clip == null)
                {
                    var carrier = AudioClip.Create("V6Carrier", 44100, 1, (int)_sampleRate, false);
                    var silence = new float[44100];
                    for (int i = 0; i < silence.Length; i++) silence[i] = 1f;
                    carrier.SetData(silence, 0);
                    src.clip = carrier;
                }
                src.playOnAwake = true;
                src.loop = true;
                src.spatialBlend = 0f;
                if (!src.isPlaying) src.Play();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ProceduralV6Audio] Audio init warning: " + ex.Message);
            }
        }

        public void TriggerShiftPop()
        {
            _shiftPopEnv = 1.0f;
        }

        public void TriggerBeep(bool highGoBeep)
        {
            _beepFreq = highGoBeep ? 1320.0 : 660.0;
            _beepEnv = 1.0f;
        }

        public void TriggerClick()
        {
            _beepFreq = 1040.0;
            _beepEnv = 0.35f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!audioEnabled)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            double baseFreq = (rpm / 60.0) * 3.0; // V6 1.6L firing order fundamental
            double inc1 = baseFreq / _sampleRate;
            double inc2 = (baseFreq * 2.0) / _sampleRate;
            double incTurbo = (1400.0 + (rpm / 13500.0) * 2900.0) / _sampleRate;
            float loadGain = engineActive ? (0.14f + throttle * 0.16f) : 0f;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;

                if (engineActive)
                {
                    _phase1 += inc1; if (_phase1 > 1.0) _phase1 -= 1.0;
                    _phase2 += inc2; if (_phase2 > 1.0) _phase2 -= 1.0;
                    _phaseTurbo += incTurbo; if (_phaseTurbo > 1.0) _phaseTurbo -= 1.0;

                    // Sawtooth + harmonic V6 rasp + MGU-H turbo whine
                    float saw = (float)(_phase1 * 2.0 - 1.0);
                    float h2 = Mathf.Sin((float)(_phase2 * 6.2831853));
                    float turbo = Mathf.Sin((float)(_phaseTurbo * 6.2831853)) * 0.14f * throttle;
                    sample += (saw * 0.55f + h2 * 0.35f + turbo) * loadGain;

                    // Noise for tyre screech & kerb rumble
                    _rng = _rng * 1664525u + 1013904223u;
                    float white = ((_rng & 0xFFFFu) / 32768f) - 1f;
                    if (slip > 0.08f)
                        sample += white * slip * 0.08f;
                    if (kerb > 0.05f)
                        sample += Mathf.Sin((float)(_phase1 * 0.5 * 6.28318)) * kerb * 0.10f;
                }

                if (_shiftPopEnv > 0.001f)
                {
                    _rng = _rng * 1664525u + 1013904223u;
                    float pop = (((_rng & 0xFFFFu) / 32768f) - 1f) * _shiftPopEnv * 0.22f;
                    sample += pop;
                    _shiftPopEnv *= 0.9965f;
                }

                if (_beepEnv > 0.001f)
                {
                    _phaseTurbo += _beepFreq / _sampleRate;
                    if (_phaseTurbo > 1.0) _phaseTurbo -= 1.0;
                    sample += Mathf.Sin((float)(_phaseTurbo * 6.2831853)) * _beepEnv * 0.20f;
                    _beepEnv *= 0.9990f;
                }

                sample = Mathf.Clamp(sample, -0.85f, 0.85f);
                for (int c = 0; c < channels; c++)
                    data[i + c] = sample;
            }
        }
    }
}
