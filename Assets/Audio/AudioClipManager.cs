using UnityEngine;

namespace Audio
{
    public class AudioClipManager : MonoBehaviour
    {
        [SerializeField]
        private int _fft_resolution = 1024;
        
        private AudioSource _source = null;
        private Boid.AudioState[] _audioState = null;

        private float[] _spectram;
        private void Start()
        {
            _source = GetComponent<AudioSource>();
            _spectram = new float[_fft_resolution];
            _audioState = new Boid.AudioState[_fft_resolution];
        }

        private void Update()
        {
            var fps = Mathf.Max(60f, 1f / Time.fixedDeltaTime);
            _source.GetSpectrumData(_spectram, 0, FFTWindow.BlackmanHarris);
            for (int i = 0; i < _fft_resolution; i++)
            {
                _audioState[i].spectram = _spectram[i];
                _audioState[i].frequency = (int)(_source.clip.frequency / fps);
            }
        }

        public Boid.AudioState[] GetAudioStateData()
        {
            return _audioState;
        }

        public int FFT_RESOLUTION => _fft_resolution;
    }
}