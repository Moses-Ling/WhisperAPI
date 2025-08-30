# Windows Whisper API Server - MVP Software Specification

**Version**: 1.0  
**Date**: December 2024  
**Target Framework**: .NET 8.0  
**Development Environment**: Visual Studio 2022  

## 1. Executive Summary

Develop a Windows-native, OpenAI-compatible Whisper speech-to-text API server that provides real-time audio transcription with automatic model management, optimized for performance and reliability.

### 1.1 Key Objectives
- **Real-time Performance**: Process 25-second audio chunks within 25-second window
- **OpenAI Compatibility**: Full API compatibility with OpenAI Whisper endpoints
- **Windows Integration**: Native Windows service support with CLI management
- **GPU Optimization**: CUDA acceleration for 8GB VRAM systems
- **Client Compatibility**: Seamless integration with existing ClioAI client

## 2. System Requirements

### 2.1 Hardware Requirements
- **Minimum**:
  - CPU: Intel i5-8th gen or AMD Ryzen 5 3600
  - RAM: 8GB
  - Storage: 10GB free space
  - GPU: Optional (CPU fallback supported)

- **Recommended**:
  - CPU: Intel i7-10th gen or AMD Ryzen 7 5800X
  - RAM: 16GB
  - Storage: SSD with 20GB free space
  - GPU: NVIDIA RTX 3060 (8GB VRAM) with CUDA 12.x

### 2.2 Software Requirements
- **OS**: Windows 10/11 (64-bit)
- **Runtime**: .NET 8.0 Runtime
- **Dependencies**: 
  - Visual C++ Redistributable 2022
  - NVIDIA CUDA Toolkit 12.x (for GPU acceleration)

## 3. Functional Requirements

### 3.1 Core API Endpoints

#### 3.1.1 Model Management
- `GET /v1/models` - List available models
- `GET /v1/models/{id}` - Get specific model information

#### 3.1.2 Audio Transcription
- `POST /v1/audio/transcriptions` - Primary transcription endpoint (multipart/form-data)
- `POST /v1/audio/transcriptions/url` - Transcribe from URL (JSON)
- `POST /v1/audio/transcriptions/base64` - Transcribe from base64 (JSON)

#### 3.1.3 Health & Configuration
- `GET /health` - Service health check
- `GET /config` - Service configuration information

### 3.2 Audio Processing Capabilities

#### 3.2.1 Supported Formats
- **Input**: WAV, MP3, M4A, FLAC, OGG
- **Optimal**: 16kHz, mono, 16-bit PCM (ClioAI optimized)
- **Max File Size**: 100MB (configurable)
- **Chunk Processing**: 5-25 second chunks

#### 3.2.2 Audio Preprocessing
- **Auto-resampling**: To 16kHz if needed (with performance logging)
- **Channel conversion**: Stereo to mono downmixing
- **Format validation**: Log optimal vs. suboptimal inputs
- **Quality preservation**: No unnecessary processing for optimal inputs

### 3.3 Model Management

#### 3.3.1 Automatic Model Download
- **On-demand download**: Download models when first requested
- **Progress reporting**: Console progress during downloads
- **Retry logic**: Robust download with exponential backoff
- **Validation**: Model integrity verification
- **Storage**: Organized in configurable models directory

#### 3.3.2 Supported Models
- **Tiny**: 39MB, fastest, lowest accuracy
- **Base**: 142MB, balanced performance (default)
- **Small**: 244MB, good accuracy
- **Medium**: 769MB, high accuracy
- **Large-v3**: 1550MB, highest accuracy
- **Language variants**: English-only models (.en)

### 3.4 Performance Requirements

#### 3.4.1 Real-time Processing
- **Target**: Process 25-second chunks in <25 seconds
- **GPU Performance**: <10 seconds for 25-second chunks (RTF < 0.4x)
- **CPU Fallback**: <25 seconds for 25-second chunks (RTF < 1.0x)
- **Memory Usage**: <4GB for base model, <8GB for large model

#### 3.4.2 Concurrency
- **Max Concurrent Requests**: 4 (configurable)
- **Queue Management**: Request queuing with timeout
- **Resource Management**: Automatic memory cleanup

### 3.5 CLI Interface

#### 3.5.1 Basic Commands
```bash
# Start server
whisper-server.exe --host localhost --port 8000 --model base --language en

# Service management
whisper-server.exe --install-service --config production.json
whisper-server.exe --service
whisper-server.exe --uninstall-service

# Utility commands
whisper-server.exe list-models
whisper-server.exe download large-v3
whisper-server.exe test audio-sample.wav
```

#### 3.5.2 Configuration Options
- **Runtime**: `--host`, `--port`, `--model`, `--language`
- **Advanced**: `--config`, `--log-level`, `--models-dir`, `--max-file-size`
- **Service**: `--service`, `--install-service`, `--uninstall-service`
- **Utility**: `--no-download`, `--help`, `--version`

## 4. Technical Architecture

### 4.1 Technology Stack

#### 4.1.1 Core Framework
- **.NET 8.0**: Modern C# with native AOT support potential
- **ASP.NET Core**: Web API framework with Kestrel server
- **Whisper.net**: C# wrapper for whisper.cpp with CUDA support
- **Serilog**: Structured logging framework

#### 4.1.2 Key Libraries
- **System.CommandLine**: CLI argument parsing
- **Microsoft.Extensions.Hosting**: Windows service support
- **NAudio**: Audio format validation and processing
- **Newtonsoft.Json**: JSON serialization (OpenAI compatibility)

### 4.2 Project Structure
```
WhisperAPI/
├── Program.cs                 # Entry point & CLI handling
├── Configuration/             # Configuration models
│   ├── ServerConfiguration.cs
│   └── AudioConfiguration.cs
├── Controllers/               # API controllers
│   ├── AudioController.cs
│   ├── ModelsController.cs
│   └── HealthController.cs
├── Services/                  # Business logic
│   ├── ModelManager.cs
│   ├── WhisperTranscriber.cs
│   ├── TranscriptionService.cs
│   ├── AudioValidationService.cs
│   └── HistoryLogger.cs
├── Models/                    # Data models
│   ├── TranscriptionModels.cs
│   └── ErrorModels.cs
└── wwwroot/                   # Static files (optional)
```

### 4.3 Configuration Management

#### 4.3.1 Configuration Sources (Priority Order)
1. **CLI Arguments** (highest priority)
2. **Environment Variables** (`WHISPER_*`)
3. **Configuration File** (JSON/YAML)
4. **Built-in Defaults** (lowest priority)

#### 4.3.2 Configuration File Structure
```json
{
  "server": {
    "host": "localhost",
    "port": 8000,
    "timeout_seconds": 300
  },
  "whisper": {
    "model_name": "base",
    "language": "en",
    "temperature": 0.01,
    "chunk_length_seconds": 25
  },
  "audio": {
    "sample_rate": 16000,
    "max_file_size_mb": 100,
    "auto_resample": false
  },
  "performance": {
    "device": "auto",
    "max_concurrent_requests": 4,
    "enable_gpu": true
  },
  "logging": {
    "level": "Information",
    "file_path": "logs/whisper-server.log"
  }
}
```

## 5. API Specification

### 5.1 Request/Response Formats

#### 5.1.1 Transcription Request (Multipart)
```http
POST /v1/audio/transcriptions HTTP/1.1
Content-Type: multipart/form-data

file: [audio file]
model: "base" (optional)
language: "en" (optional)
temperature: 0.0 (optional)
response_format: "json" (optional)
timestamp_granularities[]: "segment" (optional)
```

#### 5.1.2 Transcription Response
```json
{
  "text": "Hello, this is a transcription of the audio file.",
  "duration": 25.3,
  "language": "en",
  "segments": [
    {
      "id": 0,
      "start": 0.0,
      "end": 5.2,
      "text": "Hello, this is a transcription"
    }
  ]
}
```

#### 5.1.3 Models Response
```json
{
  "object": "list",
  "data": [
    {
      "id": "whisper-base",
      "object": "model",
      "owned_by": "openai"
    }
  ]
}
```

### 5.2 Error Handling

#### 5.2.1 Standard Error Response
```json
{
  "error": {
    "message": "Error description",
    "type": "invalid_request_error",
    "code": "file_too_large"
  }
}
```

#### 5.2.2 HTTP Status Codes
- **200**: Success
- **400**: Bad Request (invalid file, format, etc.)
- **413**: File too large
- **422**: Unsupported audio format
- **429**: Too many requests
- **500**: Internal server error
- **503**: Service unavailable (model loading)

## 6. Non-Functional Requirements

### 6.1 Performance Benchmarks

#### 6.1.1 Processing Speed Targets
| Model | GPU (RTX 3060) | CPU (i7-10700) |
|-------|----------------|----------------|
| Tiny  | <3s (0.12x RTF) | <8s (0.32x RTF) |
| Base  | <5s (0.20x RTF) | <15s (0.60x RTF) |
| Small | <8s (0.32x RTF) | <25s (1.00x RTF) |
| Medium| <12s (0.48x RTF)| <40s (1.60x RTF) |
| Large | <18s (0.72x RTF)| <60s (2.40x RTF) |

*RTF = Real-Time Factor (processing time / audio duration)*

#### 6.1.2 Resource Usage Limits
- **Memory**: <4GB for base model, <8GB for large model
- **CPU**: <80% utilization during processing
- **GPU**: <90% VRAM utilization
- **Disk**: <1GB temporary files at any time

### 6.2 Reliability Requirements

#### 6.2.1 Error Handling
- **Graceful degradation**: CPU fallback if GPU fails
- **Automatic recovery**: Restart transcription service on crashes
- **Resource cleanup**: Automatic temporary file cleanup
- **Request timeout**: 5-minute maximum per request

#### 6.2.2 Logging Requirements
- **Structured logging**: JSON format with correlation IDs
- **Performance metrics**: Processing times, RTF ratios
- **Error tracking**: Full stack traces for debugging
- **Audit trail**: All API requests and responses
- **Log rotation**: Daily rotation with 30-day retention

### 6.3 Security Requirements

#### 6.3.1 Input Validation
- **File size limits**: Configurable maximum file size
- **Format validation**: Reject unsupported formats
- **Content scanning**: Basic malware detection
- **Rate limiting**: Optional request rate limiting

#### 6.3.2 Network Security
- **CORS support**: Configurable CORS policies
- **HTTPS support**: TLS 1.2+ support (optional)
- **Local binding**: Default to localhost for security
- **API key support**: Optional API key authentication

## 7. Development Phases

### 7.1 Phase 1: Core MVP (2-3 weeks)
**Deliverables**:
- [x] Basic CLI with essential parameters
- [x] OpenAI-compatible API endpoints
- [x] Whisper.net integration with CUDA support
- [x] Automatic model downloading
- [x] Basic configuration management
- [x] Console application mode

**Acceptance Criteria**:
- Successfully transcribe ClioAI audio chunks
- Process base model within performance targets
- OpenAI API compatibility verified

### 7.2 Phase 2: Service & Optimization (1-2 weeks)
**Deliverables**:
- [ ] Windows service installation/management
- [ ] Audio format validation and optimization
- [ ] Performance monitoring and logging
- [ ] Configuration file support
- [ ] Enhanced error handling

**Acceptance Criteria**:
- Windows service installation working
- Performance metrics logging implemented
- Optimal audio path detection functional

### 7.3 Phase 3: Production Features (1 week)
**Deliverables**:
- [ ] Advanced CLI commands (test, download, etc.)
- [ ] Request queuing and concurrency control
- [ ] Comprehensive logging and monitoring
- [ ] Documentation and deployment guide

**Acceptance Criteria**:
- Production-ready deployment
- Full CLI functionality
- Performance targets met consistently

## 8. Testing Strategy

### 8.1 Unit Testing
- **Coverage Target**: >80% code coverage
- **Key Areas**: Audio processing, model management, API controllers
- **Framework**: xUnit with FluentAssertions
- **Mocking**: Moq for external dependencies

### 8.2 Integration Testing
- **API Testing**: Full OpenAI compatibility testing
- **Performance Testing**: Automated benchmarking
- **ClioAI Integration**: End-to-end testing with actual client
- **Service Testing**: Windows service lifecycle testing

### 8.3 Performance Testing
- **Load Testing**: Multiple concurrent requests
- **Stress Testing**: Memory and CPU stress testing
- **Benchmark Suite**: Automated performance regression testing
- **Real-world Testing**: 4-hour continuous operation test

## 9. Deployment & Distribution

### 9.1 Packaging
- **Self-contained**: Single executable with runtime
- **MSI Installer**: Windows Installer package
- **Portable Version**: Zip archive with all dependencies
- **Chocolatey Package**: Optional package manager distribution

### 9.2 Installation Requirements
- **Prerequisites**: Automatic VC++ Redistributable installation
- **CUDA Detection**: Optional CUDA runtime installation
- **Service Registration**: Automatic Windows service registration
- **Firewall Rules**: Optional Windows Firewall configuration

### 9.3 Documentation
- **Quick Start Guide**: 5-minute setup guide
- **API Documentation**: OpenAPI/Swagger specification
- **Configuration Reference**: Complete configuration options
- **Troubleshooting Guide**: Common issues and solutions

## 10. Success Metrics

### 10.1 Performance Metrics
- **Real-time Factor**: <1.0x for base model on recommended hardware
- **Memory Usage**: <4GB for base model
- **Startup Time**: <30 seconds including model loading
- **Request Latency**: <2 seconds API overhead

### 10.2 Reliability Metrics
- **Uptime**: >99.5% availability during continuous operation
- **Error Rate**: <1% failed transcriptions
- **Recovery Time**: <60 seconds automatic recovery from failures
- **Memory Leaks**: Zero memory leaks in 24-hour operation

### 10.3 Compatibility Metrics
- **ClioAI Integration**: 100% compatibility with existing client
- **OpenAI Compatibility**: 100% API compatibility
- **Format Support**: Support all formats used by ClioAI
- **Windows Versions**: Support Windows 10/11 (64-bit)

## 11. Risk Assessment

### 11.1 Technical Risks
| Risk | Probability | Impact | Mitigation |
|------|------------|---------|------------|
| CUDA compatibility issues | Medium | High | CPU fallback implementation |
| Whisper.net performance | Low | High | Direct whisper.cpp integration option |
| Model download failures | Medium | Medium | Retry logic and manual download option |
| Memory leaks | Low | High | Comprehensive testing and monitoring |

### 11.2 Dependencies Risks
| Dependency | Risk Level | Mitigation |
|------------|------------|------------|
| Whisper.net | Medium | Keep updated, have fallback plan |
| .NET 8.0 | Low | Mature platform, LTS support |
| CUDA Toolkit | Medium | Graceful degradation to CPU |
| NAudio | Low | Mature library, stable API |

## 12. Future Enhancements (Post-MVP)

### 12.1 Advanced Features
- **Streaming transcription**: Real-time streaming API
- **Speaker diarization**: Multi-speaker identification
- **Custom model support**: Fine-tuned model loading
- **Batch processing**: Multiple file processing
- **Web dashboard**: Browser-based management interface

### 12.2 Integration Features
- **Docker support**: Containerized deployment
- **REST webhooks**: Async result delivery
- **Database integration**: Result persistence
- **Cloud integration**: Azure/AWS deployment options
- **Monitoring integration**: Prometheus/Grafana metrics

---

## Appendices

### Appendix A: ClioAI Compatibility Matrix
| Feature | ClioAI Requirement | Server Implementation |
|---------|-------------------|----------------------|
| Audio Format | 16kHz mono PCM | ✅ Optimized detection |
| Chunk Size | 5-25 seconds | ✅ Configurable support |
| API Endpoint | `/v1/audio/transcriptions` | ✅ OpenAI compatible |
| Response Format | JSON with text field | ✅ Standard compliance |
| Error Handling | HTTP status codes | ✅ Standard error responses |

### Appendix B: Development Environment Setup
```bash
# Prerequisites
- Visual Studio 2022 (Community or higher)
- .NET 8.0 SDK
- Git for Windows
- Optional: CUDA Toolkit 12.x

# Project Setup
git clone [repository-url]
cd whisper-server
dotnet restore
dotnet build
dotnet run
```

### Appendix C: Performance Benchmarking
```bash
# Benchmark audio files
whisper-server.exe test samples/5sec.wav
whisper-server.exe test samples/25sec.wav
whisper-server.exe test samples/60sec.wav

# Performance monitoring
whisper-server.exe --log-level Debug --config benchmark.json
```

---

**Document Status**: Draft v1.0  
**Next Review**: Upon approval for development start  
**Approval Required**: Technical Lead, Product Owner