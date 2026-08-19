---
name: youtube-transcript-knowledge
description: Download YouTube video transcripts and extract key insights, patterns, and technical concepts for project knowledge
version: 1.0.0
inputs:
  - name: video_url
    type: string
    description: YouTube video URL to download and analyze
    required: true
  - name: analysis_focus
    type: array
    description: Specific aspects to focus on (architecture, patterns, best-practices, concepts, all)
    required: false
    default: all
  - name: output_location
    type: string
    description: Where to save the analyzed knowledge
    required: false
    default: .neo/memory/video-knowledge
outputs:
  - name: transcript_file
    type: string
    description: Path to the raw transcript text file
  - name: analysis_file
    type: string
    description: Path to the structured analysis markdown file
  - name: key_concepts
    type: array
    description: List of main concepts and patterns identified
tags:
  - analysis
  - knowledge-extraction
  - video
  - documentation
author: Neo Templates
last_updated: 2026-02-18
---

# YouTube Transcript Knowledge Extractor

## Purpose

Downloads transcripts from YouTube videos using yt-dlp and analyzes them to extract key technical insights, architectural patterns, and important concepts. Stores the analyzed knowledge in `.neo/memory/video-knowledge/` for agents to reference when implementing features.

## When to Use

- User provides a YouTube link to a technical tutorial or talk
- Learning about architectural patterns (e.g., sub-agent architecture, microservices)
- Extracting best practices from coding tutorials
- Documenting design patterns explained in videos
- Building project knowledge base from educational content
- Understanding framework-specific patterns and conventions

## Prerequisites

- **Terminal access/execution capability** - This skill requires the ability to run shell commands
  - **To verify**: Ask your AI assistant to run a simple command like `echo "test"`
  - **GitHub Copilot**: Terminal execution is available by default in VS Code
  - **If not available**: See "Without Terminal Access" section below
- yt-dlp installed (will attempt automatic installation if missing)
- Python 3 for post-processing transcript deduplication
- Internet connection for downloading transcripts
- Optional: Whisper for videos without captions (requires user confirmation)

### With Terminal Access

If your AI assistant can execute terminal commands (standard in GitHub Copilot), this skill will:
1. Automatically download transcripts using yt-dlp
2. Process and deduplicate the text
3. Analyze the content
4. Create structured knowledge files

### Without Terminal Access

If terminal execution is not available, follow this manual workflow:

1. **Download the transcript yourself:**
   ```bash
   # Option 1: Using yt-dlp (install from https://github.com/yt-dlp/yt-dlp)
   yt-dlp --write-auto-sub --skip-download "YOUR_VIDEO_URL"
   
   # Option 2: Browser extension (search for "YouTube Transcript" in your browser's extension store)
   
   # Option 3: Online tools (e.g., youtubetranscript.com)
   ```

2. **Provide the transcript text to the AI** by pasting it or saving it to a file

3. **The AI will analyze it** and create the structured knowledge file

4. **Save the files** to `.neo/memory/video-knowledge/[video-id]-[title].md`

## Inputs

### video_url
- **Type**: string
- **Required**: Yes
- **Description**: Full YouTube video URL (format: `https://www.youtube.com/watch?v=VIDEO_ID`)
- **Example**: `https://www.youtube.com/watch?v=dQw4w9WgXcQ`

### analysis_focus
- **Type**: array
- **Required**: No
- **Default**: `["all"]`
- **Description**: Specific aspects to focus on during analysis. Options: `architecture`, `patterns`, `best-practices`, `concepts`, `all`
- **Example**: `["architecture", "patterns"]`

### output_location
- **Type**: string
- **Required**: No
- **Default**: `.neo/memory/video-knowledge`
- **Description**: Directory where analyzed knowledge will be stored
- **Example**: `.neo/memory/video-knowledge`

## Outputs

### transcript_file
- **Type**: string
- **Description**: Path to the cleaned, deduplicated transcript text file
- **Example**: `.neo/memory/video-knowledge/dQw4w9WgXcQ-video-title.txt`

### analysis_file
- **Type**: string
- **Description**: Path to the structured markdown analysis with extracted concepts and patterns
- **Example**: `.neo/memory/video-knowledge/dQw4w9WgXcQ-video-title.md`

### key_concepts
- **Type**: array
- **Description**: List of main technical concepts, patterns, and insights identified in the video
- **Example**: 
```json
[
  "Sub-agent architecture for complex workflows",
  "Event-driven communication between agents",
  "State management in distributed systems"
]
```

## Implementation Steps

### Step 1: Check and Install yt-dlp

**Always check if yt-dlp is installed first:**

```bash
which yt-dlp || command -v yt-dlp
```

**If not installed, attempt automatic installation:**

```bash
# macOS (Homebrew)
if command -v brew &> /dev/null; then
    brew install yt-dlp
# Linux (apt/Debian/Ubuntu)
elif command -v apt &> /dev/null; then
    sudo apt update && sudo apt install -y yt-dlp
# Alternative (pip - works on all systems)
else
    pip3 install yt-dlp
fi
```

**If installation fails:** Inform the user to install manually from https://github.com/yt-dlp/yt-dlp#installation

### Step 2: Extract Video ID and Check Available Subtitles

**Extract video ID from URL:**
```bash
VIDEO_URL="https://www.youtube.com/watch?v=dQw4w9WgXcQ"
VIDEO_ID=$(echo "$VIDEO_URL" | sed -n 's/.*[?&]v=\([^&]*\).*/\1/p')
```

**ALWAYS check available subtitles first:**
```bash
yt-dlp --list-subs "$VIDEO_URL"
```

This shows what subtitle types are available without downloading anything. Look for:
- Manual subtitles (better quality)
- Auto-generated subtitles (usually available)
- Available languages

### Step 3: Download Transcript (Priority Order)

**Get video title for filename:**
```bash
VIDEO_TITLE=$(yt-dlp --print "%(title)s" "$VIDEO_URL" | tr '/' '_' | tr ':' '-' | tr '?' '' | tr '"' '' | tr '<' '' | tr '>' '')
OUTPUT_NAME="transcript_temp"
```

**Option 1: Manual Subtitles (Preferred - highest quality)**
```bash
echo "Attempting to download manual subtitles..."
if yt-dlp --write-sub --skip-download --output "$OUTPUT_NAME" "$VIDEO_URL" 2>/dev/null; then
    echo "✓ Manual subtitles downloaded successfully!"
    ls -lh ${OUTPUT_NAME}.*
fi
```

**Option 2: Auto-Generated Subtitles (Fallback)**
```bash
echo "Manual subtitles not available. Trying auto-generated..."
if yt-dlp --write-auto-sub --skip-download --output "$OUTPUT_NAME" "$VIDEO_URL" 2>/dev/null; then
    echo "✓ Auto-generated subtitles downloaded successfully!"
    ls -lh ${OUTPUT_NAME}.*
fi
```

**Option 3: Whisper Transcription (Last Resort - requires user confirmation)**

Only use if both manual and auto-generated subtitles are unavailable.

```bash
# Get file size estimate
FILE_SIZE=$(yt-dlp --print "%(filesize_approx)s" -f "bestaudio" "$VIDEO_URL")
DURATION=$(yt-dlp --print "%(duration)s" "$VIDEO_URL")

echo "⚠ No subtitles available for this video."
echo "Video: $VIDEO_TITLE"
echo "Duration: $((DURATION / 60)) minutes"
echo "Audio size: ~$((FILE_SIZE / 1024 / 1024)) MB"
echo ""
echo "Would you like to download and transcribe with Whisper? (y/n)"
```

**If user confirms:**

1. Check for Whisper installation:
```bash
if ! command -v whisper &> /dev/null; then
    echo "Whisper not installed. Install now? (requires ~1-3GB) (y/n)"
    # If user confirms:
    pip3 install openai-whisper
fi
```

2. Download audio:
```bash
yt-dlp -x --audio-format mp3 --output "audio_%(id)s.%(ext)s" "$VIDEO_URL"
```

3. Transcribe with Whisper:
```bash
AUDIO_FILE=$(ls audio_*.mp3 | head -n 1)
whisper "$AUDIO_FILE" --model base --output_format vtt
```

4. Cleanup (ask user first):
```bash
echo "Transcription complete! Delete audio file? (y/n)"
# If yes: rm "$AUDIO_FILE"
```

### Step 4: Convert to Plain Text (Deduplicate VTT)

YouTube's auto-generated VTT files contain duplicate lines because captions are shown progressively. Always deduplicate:

```bash
# Find the VTT file
VTT_FILE=$(ls ${OUTPUT_NAME}*.vtt 2>/dev/null || ls *.vtt | head -n 1)

# Convert with deduplication
python3 -c "
import sys, re
seen = set()
with open('$VTT_FILE', 'r') as f:
    for line in f:
        line = line.strip()
        if line and not line.startswith('WEBVTT') and not line.startswith('Kind:') and not line.startswith('Language:') and '-->' not in line:
            clean = re.sub('<[^>]*>', '', line)
            clean = clean.replace('&amp;', '&').replace('&gt;', '>').replace('&lt;', '<')
            if clean and clean not in seen:
                print(clean)
                seen.add(clean)
" > "${VIDEO_ID}-${VIDEO_TITLE}.txt"

echo "✓ Saved transcript to: ${VIDEO_ID}-${VIDEO_TITLE}.txt"

# Clean up temporary VTT file
rm "$VTT_FILE"
echo "✓ Cleaned up temporary VTT file"
```

### Step 5: Move Transcript to Knowledge Directory

```bash
# Ensure output directory exists
mkdir -p .neo/memory/video-knowledge

# Move transcript file
mv "${VIDEO_ID}-${VIDEO_TITLE}.txt" ".neo/memory/video-knowledge/"
TRANSCRIPT_PATH=".neo/memory/video-knowledge/${VIDEO_ID}-${VIDEO_TITLE}.txt"

echo "✓ Transcript saved to: $TRANSCRIPT_PATH"
```

### Step 6: Analyze Transcript and Extract Knowledge

Read the transcript and analyze it based on the `analysis_focus`:

**For architecture analysis:**
- Identify system components and their relationships
- Extract architectural patterns (microservices, event-driven, layered, etc.)
- Note scalability and design considerations

**For design patterns:**
- Identify named patterns (Singleton, Factory, Observer, etc.)
- Extract pattern implementations and use cases
- Note when and why to use each pattern

**For best practices:**
- Extract coding conventions and standards
- Identify recommended approaches vs anti-patterns
- Note tooling and workflow recommendations

**For concepts:**
- List key technical terms and definitions
- Extract core principles and theories
- Note important relationships between concepts

### Step 7: Create Structured Analysis Document

Create a markdown file with the analyzed knowledge:

```markdown
---
video_id: [VIDEO_ID]
video_title: [VIDEO_TITLE]
video_url: [VIDEO_URL]
analyzed_date: [CURRENT_DATE]
analysis_focus: [architecture|patterns|best-practices|concepts|all]
---

# [VIDEO_TITLE]

## Video Information
- **URL**: [VIDEO_URL]
- **Duration**: [DURATION]
- **Analyzed**: [DATE]

## Summary
[2-3 sentence summary of the video's main topic and purpose]

## Key Concepts
[List of main technical concepts explained]

### Concept 1: [Name]
- **Definition**: [Clear definition]
- **Use Cases**: [When to apply]
- **Related Concepts**: [Links to other concepts]

### Concept 2: [Name]
...

## Architectural Patterns
[If applicable - patterns discussed in the video]

### Pattern 1: [Name]
- **Description**: [What it is]
- **Components**: [Key parts]
- **Benefits**: [Advantages]
- **Trade-offs**: [Considerations]
- **Implementation Notes**: [How to implement]

## Best Practices
[Key recommendations and guidelines from the video]

1. **[Practice 1]**: [Description and rationale]
2. **[Practice 2]**: [Description and rationale]

## Code Examples
[If the video includes code examples, extract and document them]

```language
[code example]
```

## Action Items
[Concrete next steps or things to implement based on the video]

- [ ] [Action item 1]
- [ ] [Action item 2]

## References
- Original Video: [VIDEO_URL]
- Transcript: [TRANSCRIPT_PATH]
- Related Documentation: [If mentioned in video]

## Related Skills
- [Links to relevant .github/skills/ that could help implement concepts]

## Notes
[Additional observations, caveats, or context]
```

**Save the analysis:**

```bash
ANALYSIS_PATH=".neo/memory/video-knowledge/${VIDEO_ID}-${VIDEO_TITLE}.md"
# Write the structured analysis to the file
echo "✓ Analysis saved to: $ANALYSIS_PATH"
```

### Step 8: Confirm Completion and Next Steps

```bash
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✓ YouTube Transcript Knowledge Extraction Complete!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Files created:"
echo "  Transcript: $TRANSCRIPT_PATH"
echo "  Analysis:   $ANALYSIS_PATH"
echo ""
echo "To reference this knowledge in agent prompts, use:"
echo "  #file:$ANALYSIS_PATH"
echo ""
echo "Key concepts identified:"
# List the main concepts extracted
```

## Usage Examples

### Example 1: Analyze Architecture Tutorial

```markdown
Using skill: youtube-transcript-knowledge

Input:
- video_url: https://www.youtube.com/watch?v=ABC123
- analysis_focus: ["architecture", "patterns"]
- output_location: .neo/memory/video-knowledge

Process:
1. ✓ yt-dlp installed
2. ✓ Checked available subtitles: auto-generated English available
3. ✓ Downloaded auto-generated subtitles
4. ✓ Converted to plain text (deduplicated 847 lines to 312 unique)
5. ✓ Saved transcript: .neo/memory/video-knowledge/ABC123-microservices-architecture-explained.txt
6. ✓ Analyzed for architecture and patterns
7. ✓ Created structured analysis: .neo/memory/video-knowledge/ABC123-microservices-architecture-explained.md

Key concepts identified:
- Microservices architecture pattern
- API Gateway pattern
- Service discovery mechanisms
- Event-driven communication
- Database per service pattern

Next steps:
- Reference: #file:.neo/memory/video-knowledge/ABC123-microservices-architecture-explained.md
- Use concepts when planning feature architecture
```

### Example 2: Extract Design Patterns

```markdown
Using skill: youtube-transcript-knowledge

Input:
- video_url: https://www.youtube.com/watch?v=XYZ789
- analysis_focus: ["patterns", "best-practices"]

Process:
1. ✓ Manual subtitles downloaded (higher quality)
2. ✓ Transcript saved and deduplicated
3. ✓ Analyzed for design patterns and best practices

Key patterns extracted:
- Factory Pattern: Creating objects without specifying exact class
- Observer Pattern: One-to-many dependency notification
- Strategy Pattern: Selecting algorithm at runtime

Best practices:
- Favor composition over inheritance
- Program to interfaces, not implementations
- Keep classes focused (Single Responsibility)

Output:
- Analysis file: .neo/memory/video-knowledge/XYZ789-design-patterns-tutorial.md
```

### Example 3: Fallback to Whisper

```markdown
Using skill: youtube-transcript-knowledge

Input:
- video_url: https://www.youtube.com/watch?v=OLD123 (old video, no captions)

Process:
1. ✓ yt-dlp installed
2. ⚠ No manual subtitles available
3. ⚠ No auto-generated subtitles available
4. ℹ Video duration: 18 minutes, audio size: ~12 MB
5. ? User confirmed Whisper transcription
6. ✓ Whisper installed
7. ✓ Audio downloaded
8. ⏳ Transcribing with Whisper (base model)... [2 minutes]
9. ✓ Transcription complete
10. ✓ Audio file deleted (user confirmed)
11. ✓ Transcript processed and saved
12. ✓ Analysis created

Result: Successfully extracted knowledge from video without captions
```

## Complete Workflow Script

Here's a complete bash script implementing all steps:

```bash
#!/bin/bash

VIDEO_URL="$1"
ANALYSIS_FOCUS="${2:-all}"
OUTPUT_DIR="${3:-.neo/memory/video-knowledge}"

# Validate input
if [ -z "$VIDEO_URL" ]; then
    echo "Error: Please provide a YouTube URL"
    exit 1
fi

# ============================================
# STEP 1: Check if yt-dlp is installed
# ============================================
if ! command -v yt-dlp &> /dev/null; then
    echo "yt-dlp not found, attempting to install..."
    if command -v brew &> /dev/null; then
        brew install yt-dlp
    elif command -v apt &> /dev/null; then
        sudo apt update && sudo apt install -y yt-dlp
    else
        pip3 install yt-dlp
    fi
fi

# ============================================
# STEP 2: Extract video info
# ============================================
echo "Fetching video information..."
VIDEO_ID=$(echo "$VIDEO_URL" | sed -n 's/.*[?&]v=\([^&]*\).*/\1/p')
VIDEO_TITLE=$(yt-dlp --print "%(title)s" "$VIDEO_URL" | tr '/' '_' | tr ':' '-' | tr '?' '' | tr '"' '' | tr '<' '' | tr '>' '')
OUTPUT_NAME="transcript_temp"

echo "Video: $VIDEO_TITLE"
echo "Video ID: $VIDEO_ID"
echo ""

# ============================================
# STEP 3: Check available subtitles
# ============================================
echo "Checking available subtitles..."
yt-dlp --list-subs "$VIDEO_URL"
echo ""

# ============================================
# STEP 4: Try manual subtitles first
# ============================================
echo "Attempting to download manual subtitles..."
if yt-dlp --write-sub --skip-download --output "$OUTPUT_NAME" "$VIDEO_URL" 2>/dev/null; then
    echo "✓ Manual subtitles downloaded successfully!"
else
    # ============================================
    # STEP 5: Fallback to auto-generated
    # ============================================
    echo "Manual subtitles not available. Trying auto-generated..."
    if yt-dlp --write-auto-sub --skip-download --output "$OUTPUT_NAME" "$VIDEO_URL" 2>/dev/null; then
        echo "✓ Auto-generated subtitles downloaded successfully!"
    else
        # ============================================
        # STEP 6: Last resort - Whisper
        # ============================================
        echo "⚠ No subtitles available for this video."
        
        FILE_SIZE=$(yt-dlp --print "%(filesize_approx)s" -f "bestaudio" "$VIDEO_URL" 2>/dev/null || echo "0")
        DURATION=$(yt-dlp --print "%(duration)s" "$VIDEO_URL" 2>/dev/null || echo "0")
        
        echo "Video: $VIDEO_TITLE"
        echo "Duration: $((DURATION / 60)) minutes"
        echo "Audio size: ~$((FILE_SIZE / 1024 / 1024)) MB"
        echo ""
        echo "Would you like to download and transcribe with Whisper? (y/n)"
        read -r RESPONSE
        
        if [[ "$RESPONSE" =~ ^[Yy]$ ]]; then
            # Check for Whisper
            if ! command -v whisper &> /dev/null; then
                echo "Whisper not installed. Install now? (requires ~1-3GB) (y/n)"
                read -r INSTALL_RESPONSE
                if [[ "$INSTALL_RESPONSE" =~ ^[Yy]$ ]]; then
                    pip3 install openai-whisper
                else
                    echo "Cannot proceed without Whisper. Exiting."
                    exit 1
                fi
            fi
            
            # Download audio
            echo "Downloading audio..."
            yt-dlp -x --audio-format mp3 --output "audio_%(id)s.%(ext)s" "$VIDEO_URL"
            
            AUDIO_FILE=$(ls audio_*.mp3 | head -n 1)
            
            # Transcribe
            echo "Transcribing with Whisper (this may take a few minutes)..."
            whisper "$AUDIO_FILE" --model base --output_format vtt
            
            # Cleanup
            echo "Transcription complete! Delete audio file? (y/n)"
            read -r CLEANUP_RESPONSE
            if [[ "$CLEANUP_RESPONSE" =~ ^[Yy]$ ]]; then
                rm "$AUDIO_FILE"
                echo "Audio file deleted."
            fi
        else
            echo "Transcription cancelled."
            exit 0
        fi
    fi
fi

# ============================================
# STEP 7: Convert to plain text with deduplication
# ============================================
VTT_FILE=$(ls ${OUTPUT_NAME}*.vtt 2>/dev/null || ls *.vtt | head -n 1)

if [ -f "$VTT_FILE" ]; then
    echo "Converting to plain text and removing duplicates..."
    python3 -c "
import sys, re
seen = set()
with open('$VTT_FILE', 'r') as f:
    for line in f:
        line = line.strip()
        if line and not line.startswith('WEBVTT') and not line.startswith('Kind:') and not line.startswith('Language:') and '-->' not in line:
            clean = re.sub('<[^>]*>', '', line)
            clean = clean.replace('&amp;', '&').replace('&gt;', '>').replace('&lt;', '<')
            if clean and clean not in seen:
                print(clean)
                seen.add(clean)
" > "${VIDEO_ID}-${VIDEO_TITLE}.txt"
    
    # Move to knowledge directory
    mkdir -p "$OUTPUT_DIR"
    mv "${VIDEO_ID}-${VIDEO_TITLE}.txt" "$OUTPUT_DIR/"
    TRANSCRIPT_PATH="$OUTPUT_DIR/${VIDEO_ID}-${VIDEO_TITLE}.txt"
    
    echo "✓ Saved to: $TRANSCRIPT_PATH"
    
    # Clean up temporary VTT file
    rm "$VTT_FILE"
    echo "✓ Cleaned up temporary VTT file"
else
    echo "⚠ No VTT file found to convert"
    exit 1
fi

# ============================================
# STEP 8: Analyze transcript
# ============================================
echo ""
echo "Analyzing transcript for: $ANALYSIS_FOCUS"
echo "Reading transcript..."

# Now analyze the transcript content and create structured markdown
# This step requires reading the transcript and using AI capabilities
# to extract concepts, patterns, and create the structured analysis

ANALYSIS_PATH="$OUTPUT_DIR/${VIDEO_ID}-${VIDEO_TITLE}.md"
echo "Creating structured analysis: $ANALYSIS_PATH"

# The analysis content would be generated based on reading the transcript
# and extracting key information according to the analysis_focus

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✓ YouTube Transcript Knowledge Extraction Complete!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Files created:"
echo "  Transcript: $TRANSCRIPT_PATH"
echo "  Analysis:   $ANALYSIS_PATH"
echo ""
echo "To reference this knowledge in agent prompts, use:"
echo "  #file:$ANALYSIS_PATH"
```

## Error Handling

### Common Issues and Solutions

1. **yt-dlp not installed**
   - Attempt automatic installation based on system (Homebrew/apt/pip)
   - If installation fails, provide manual installation link
   - Verify installation with `which yt-dlp` before proceeding

2. **No subtitles available**
   - Always check with `--list-subs` first
   - Try both `--write-sub` (manual) and `--write-auto-sub` (automatic)
   - If both fail, offer Whisper transcription with user confirmation
   - Show file size and duration before downloading audio

3. **Invalid or private video**
   - Check URL format: `https://www.youtube.com/watch?v=VIDEO_ID`
   - Videos may be private, age-restricted, or geo-blocked
   - Display specific error from yt-dlp to user

4. **Whisper installation fails**
   - May require system dependencies (ffmpeg, rust)
   - Provide manual installation instructions
   - Check available disk space (models require 1-10GB)

5. **Download interrupted or failed**
   - Check internet connection
   - Verify sufficient disk space
   - Try with `--no-check-certificate` if SSL issues
   - Retry with exponential backoff

6. **Multiple subtitle languages**
   - By default, yt-dlp downloads all available languages
   - Can specify with `--sub-langs en` for English only
   - Always list available languages with `--list-subs` first

7. **VTT conversion errors**
   - Ensure Python 3 is available
   - Check transcript file exists before processing
   - Verify file encoding (UTF-8 expected)

8. **Analysis quality issues**
   - If transcript is incomplete, mention gaps in analysis
   - Note if audio quality affected transcription accuracy
   - Flag uncertainty about technical terms

### Best Practices

- ✅ Always check what's available before attempting download (`--list-subs`)
- ✅ Verify success at each step before proceeding
- ✅ Ask user before large downloads (audio files, Whisper models)
- ✅ Clean up temporary files after processing
- ✅ Provide clear feedback about what's happening at each stage
- ✅ Handle errors gracefully with helpful messages
- ✅ Validate video URL format before processing
- ✅ Sanitize filenames to avoid filesystem issues
- ✅ Store both raw transcript and structured analysis
- ✅ Include metadata (video URL, date, duration) in analysis

## Related Skills

- `documentation/generate-jsdoc.md` - Create documentation from extracted concepts
- `code-analysis/detect-patterns.md` - Identify patterns in code matching video concepts
- `refactoring/extract-function.md` - Apply refactoring patterns from videos

## Notes

- YouTube's auto-generated captions are usually available but may contain errors
- Manual subtitles (when available) are higher quality and preferred
- Whisper transcription is resource-intensive (CPU/GPU) and time-consuming
- Some videos may have multiple language options - English is preferred by default
- Store both raw transcript and analyzed knowledge for future reference
- Reference analyzed videos in agent prompts like: `#file:.neo/memory/video-knowledge/VIDEO_ID-title.md`
- Videos explaining architecture or patterns are especially valuable for project knowledge
- Consider updating `.neo/memory/constitution.md` if video introduces new project principles
