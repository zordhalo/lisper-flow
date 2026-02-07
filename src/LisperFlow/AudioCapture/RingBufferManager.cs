namespace LisperFlow.AudioCapture;

/// <summary>
/// Thread-safe circular buffer for audio samples
/// </summary>
public class RingBufferManager
{
    private readonly float[] _buffer;
    private readonly int _capacity;
    private int _writePosition;
    private int _readPosition;
    private int _availableSamples;
    private readonly object _lock = new();
    
    /// <summary>
    /// Capacity in samples
    /// </summary>
    public int Capacity => _capacity;
    
    /// <summary>
    /// Number of samples currently available to read
    /// </summary>
    public int AvailableSamples
    {
        get
        {
            lock (_lock)
            {
                return _availableSamples;
            }
        }
    }
    
    /// <summary>
    /// Create a new ring buffer
    /// </summary>
    /// <param name="capacityInSeconds">Buffer capacity in seconds</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    public RingBufferManager(int capacityInSeconds, int sampleRate)
    {
        _capacity = capacityInSeconds * sampleRate;
        _buffer = new float[_capacity];
    }
    
    /// <summary>
    /// Create a new ring buffer with specified sample capacity
    /// </summary>
    /// <param name="capacityInSamples">Buffer capacity in samples</param>
    public RingBufferManager(int capacityInSamples)
    {
        _capacity = capacityInSamples;
        _buffer = new float[_capacity];
    }
    
    /// <summary>
    /// Write samples to the buffer
    /// </summary>
    public void Write(float[] samples, int count)
    {
        if (count <= 0) return;
        
        lock (_lock)
        {
            for (int i = 0; i < count && i < samples.Length; i++)
            {
                _buffer[_writePosition] = samples[i];
                _writePosition = (_writePosition + 1) % _capacity;
                
                if (_availableSamples < _capacity)
                {
                    _availableSamples++;
                }
                else
                {
                    // Overwrite oldest data, advance read position
                    _readPosition = (_readPosition + 1) % _capacity;
                }
            }
        }
    }
    
    /// <summary>
    /// Write samples from a span
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_lock)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                _buffer[_writePosition] = samples[i];
                _writePosition = (_writePosition + 1) % _capacity;
                
                if (_availableSamples < _capacity)
                {
                    _availableSamples++;
                }
                else
                {
                    _readPosition = (_readPosition + 1) % _capacity;
                }
            }
        }
    }
    
    /// <summary>
    /// Read samples from the buffer
    /// </summary>
    /// <returns>Number of samples actually read</returns>
    public int Read(float[] destination, int count)
    {
        lock (_lock)
        {
            int samplesToRead = Math.Min(count, _availableSamples);
            
            for (int i = 0; i < samplesToRead; i++)
            {
                destination[i] = _buffer[_readPosition];
                _readPosition = (_readPosition + 1) % _capacity;
            }
            
            _availableSamples -= samplesToRead;
            return samplesToRead;
        }
    }
    
    /// <summary>
    /// Read all available samples
    /// </summary>
    public float[] ReadAll()
    {
        lock (_lock)
        {
            var result = new float[_availableSamples];
            
            for (int i = 0; i < _availableSamples; i++)
            {
                result[i] = _buffer[_readPosition];
                _readPosition = (_readPosition + 1) % _capacity;
            }
            
            _availableSamples = 0;
            return result;
        }
    }
    
    /// <summary>
    /// Get the last N samples (pre-roll) without consuming them
    /// </summary>
    public float[] GetPreRoll(int sampleCount)
    {
        lock (_lock)
        {
            int available = Math.Min(sampleCount, _availableSamples);
            var preRoll = new float[available];
            
            // Calculate position of oldest sample we want
            int startPos = (_writePosition - available + _capacity) % _capacity;
            
            for (int i = 0; i < available; i++)
            {
                preRoll[i] = _buffer[startPos];
                startPos = (startPos + 1) % _capacity;
            }
            
            return preRoll;
        }
    }
    
    /// <summary>
    /// Peek at samples without removing them
    /// </summary>
    public float[] Peek(int count)
    {
        lock (_lock)
        {
            int samplesToPeek = Math.Min(count, _availableSamples);
            var result = new float[samplesToPeek];
            
            int pos = _readPosition;
            for (int i = 0; i < samplesToPeek; i++)
            {
                result[i] = _buffer[pos];
                pos = (pos + 1) % _capacity;
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Clear all buffered samples
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _writePosition = 0;
            _readPosition = 0;
            _availableSamples = 0;
            Array.Clear(_buffer);
        }
    }
}
