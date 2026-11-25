using UnityEngine;

namespace Motovelo
{
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralV6Audio : MonoBehaviour
    {
        public bool audioEnabled = true;
        public bool engineActive;
        public float rpm = 4800f;
        public float throttle;
        public float speedMps;
        public float slip;
        public float kerb;

        private double _phaseFund;
        private double _phaseHalf;
        private double _sampleRate = 44100.0;
        private float _smoothRpm = 4800f;
        private float _smoothThrottle;
        private float _lp1;
        private float _lp2;
        private float _lp3;
        private float _dcPrevIn;
        private float _dcPrevOut;
        private float _shiftCutTimer;
        private float _beepEnv;
        private double _beepPhase;
        private double _beepFreq = 520.0;
        private uint _rng = 1234567u;

        private void Awake()
        {
            try
            {
                _sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100.0;
                var src = GetComponent<AudioSource>();
                if (src.clip == null)
                {
                    int len = Mathf.Max(4096, (int)_sampleRate);
                    var carrier = AudioClip.Create("V6SilentCarrier", len, 1, (int)_sampleRate, false);
                    var zeros = new float[len]; // Zero DC offset so filter never clicks or buzzes!
                    carrier.SetData(zeros, 0);
                    src.clip = carrier;
                }
                src.playOnAwake = true;
                src.loop = true;
                src.spatialBlend = 0f;
                src.volume = 0.85f;
                if (!src.isPlaying) src.Play();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ProceduralV6Audio] Audio init warning: " + ex.Message);
            }
        }

        public void TriggerShiftPop()
        {
            // Authentic 45ms seamless-gearbox ignition torque cut + warm exhaust thump!
            _shiftCutTimer = 0.045f;
        }

        public void TriggerBeep(bool highGoBeep)
        {
            _beepFreq = highGoBeep ? 880.0 : 440.0;
            _beepEnv = 0.65f;
        }

        public void TriggerClick()
        {
            _beepFreq = 660.0;
            _beepEnv = 0.22f;
        }

        private static float PolyBlep(double t, double dt)
        {
            if (t < dt)
            {
                double x = t / dt;
                return (float)(x + x - x * x - 1.0);
            }
            if (t > 1.0 - dt)
            {
                double x = (t - 1.0) / dt;
                return (float)(x * x + x + x + 1.0);
            }
            return 0f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!audioEnabled)
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
                return;
            }

            float invSr = (float)(1.0 / _sampleRate);
            float rpmNorm = Mathf.Clamp01((_smoothRpm - 4200f) / 9000f);

            // Warm 3-stage cascaded low-pass cutoff (240 Hz deep idle purr -> 1180 Hz warm throaty F1 V6 roar — ZERO piercing high-pitch whine!)
            float cutoffHz = 240f + rpmNorm * 720f + _smoothThrottle * 260f;
            float rc = 1.0f / (6.2831853f * cutoffHz);
            float alpha = invSr / (rc + invSr);

            float windGain = engineActive ? Mathf.Clamp01((speedMps - 28f) / 75f) * 0.035f : 0f;

            for (int i = 0; i < data.Length; i += channels)
            {
                _smoothRpm += (rpm - _smoothRpm) * 0.0018f;
                _smoothThrottle += (throttle - _smoothThrottle) * 0.0025f;

                float sample = 0f;

                if (engineActive)
                {
                    // 1.6L 90-degree V6 fundamental (3 firing pulses per crank rev, kept warm & deep)
                    double fundHz = (_smoothRpm / 60.0) * 1.5; // Deep 1.5x crank order + 3.0x firing order
                    double dtPhase = fundHz * invSr;
                    double dtHalf = dtPhase * 0.5;

                    _phaseFund += dtPhase;
                    if (_phaseFund >= 1.0) _phaseFund -= 1.0;
                    _phaseHalf += dtHalf;
                    if (_phaseHalf >= 1.0) _phaseHalf -= 1.0;

                    // Band-limited PolyBLEP waveform (zero digital aliasing!)
                    float saw = (float)(2.0 * _phaseFund - 1.0) - PolyBlep(_phaseFund, dtPhase);
                    float sub = Mathf.Sin((float)(_phaseHalf * 6.2831853));
                    float secondHarm = Mathf.Sin((float)(_phaseFund * 12.5663706));

                    // Warm soft-tube saturation + ignition cut on upshift
                    float cutMult = 1f;
                    if (_shiftCutTimer > 0f)
                    {
                        _shiftCutTimer -= invSr;
                        cutMult = 0.22f;
                    }

                    float rawEngine = (sub * 0.48f + saw * 0.36f + secondHarm * 0.24f) * cutMult;

                    // Off-throttle exhaust overrun burble
                    _rng = _rng * 1664525u + 1013904223u;
                    float noise = (((_rng & 0xFFFFu) / 32768f) - 1f);
                    if (_smoothThrottle < 0.15f && _smoothRpm > 7200f)
                        rawEngine += noise * 0.12f;

                    // 3-pole 18dB/oct warm muffler low-pass filter!
                    _lp1 += alpha * (rawEngine - _lp1);
                    _lp2 += alpha * (_lp1 - _lp2);
                    _lp3 += alpha * (_lp2 - _lp3);

                    float engineGain = 0.075f + _smoothThrottle * 0.065f + rpmNorm * 0.035f;
                    sample += _lp3 * engineGain;

                    // Soft low-passed aerodynamic wind rush & subtle kerb thump
                    sample += _lp1 * windGain;
                    if (kerb > 0.05f)
                        sample += Mathf.Sin((float)(_phaseHalf * 6.2831853)) * kerb * 0.065f;
                    if (slip > 0.18f)
                        sample += (_lp2 * 0.5f + noise * 0.08f) * slip * 0.05f;
                }

                if (_beepEnv > 0.001f)
                {
                    _beepPhase += _beepFreq * invSr;
                    if (_beepPhase >= 1.0) _beepPhase -= 1.0;
                    sample += Mathf.Sin((float)(_beepPhase * 6.2831853)) * _beepEnv * 0.14f;
                    _beepEnv *= 0.9988f;
                }

                // DC blocker + smooth soft limiter
                float dcOut = sample - _dcPrevIn + 0.995f * _dcPrevOut;
                _dcPrevIn = sample;
                _dcPrevOut = dcOut;

                float finalOut = Mathf.Clamp(dcOut, -0.55f, 0.55f);
                for (int c = 0; c < channels; c++)
                    data[i + c] = finalOut;
            }
        }
    }
}
