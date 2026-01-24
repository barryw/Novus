#!/bin/bash
# release.sh - Build and package Novus releases
# Creates zipped release packages with SHA256 checksums included

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RELEASE_DIR="$PROJECT_ROOT/release"
VERSION="${1:-}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

info() { echo -e "${GREEN}[INFO]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

usage() {
    echo "Usage: $0 <version> [--build] [--package-only] [--upload]"
    echo ""
    echo "Arguments:"
    echo "  version       Version tag (e.g., v0.1.0)"
    echo ""
    echo "Options:"
    echo "  --build       Build binaries for all platforms"
    echo "  --package-only  Only create zip packages from existing binaries"
    echo "  --upload      Upload to GitHub release after packaging"
    echo ""
    echo "Examples:"
    echo "  $0 v0.1.0 --build           # Build and package"
    echo "  $0 v0.1.0 --package-only    # Package existing binaries"
    echo "  $0 v0.1.0 --build --upload  # Build, package, and upload"
    exit 1
}

# Parse arguments
BUILD=false
PACKAGE_ONLY=false
UPLOAD=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --build)
            BUILD=true
            shift
            ;;
        --package-only)
            PACKAGE_ONLY=true
            shift
            ;;
        --upload)
            UPLOAD=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            if [[ -z "$VERSION" ]]; then
                VERSION="$1"
            fi
            shift
            ;;
    esac
done

if [[ -z "$VERSION" ]]; then
    error "Version is required"
    usage
fi

# Validate version format
if [[ ! "$VERSION" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    warn "Version '$VERSION' doesn't match expected format (vX.Y.Z)"
fi

info "Preparing release $VERSION"

# Create release directory
mkdir -p "$RELEASE_DIR"

# Target configurations: name, rid (runtime identifier)
declare -a TARGETS=(
    "novus-macos-arm64:osx-arm64"
    "novus-macos-x64:osx-x64"
    "novus-linux-x64:linux-x64"
    "novus-linux-arm64:linux-arm64"
    "novus-win-x64:win-x64"
)

build_binary() {
    local name="$1"
    local rid="$2"
    local output_path="$RELEASE_DIR/$name"

    info "Building $name ($rid)..."

    cd "$PROJECT_ROOT"

    # Build native AOT binary
    dotnet publish Novus/Novus.csproj \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishAot=true \
        -p:StripSymbols=true \
        -o "$RELEASE_DIR/build-$rid"

    # Copy the binary
    if [[ "$rid" == win-* ]]; then
        cp "$RELEASE_DIR/build-$rid/novus.exe" "$output_path.exe"
    else
        cp "$RELEASE_DIR/build-$rid/novus" "$output_path"
        chmod +x "$output_path"
    fi

    # Clean up build directory
    rm -rf "$RELEASE_DIR/build-$rid"

    info "Built $name"
}

generate_sha256() {
    local file="$1"
    local sha_file="$file.sha256"

    if [[ ! -f "$file" ]]; then
        error "File not found: $file"
    fi

    # Generate SHA256 (works on both macOS and Linux)
    if command -v shasum &> /dev/null; then
        shasum -a 256 "$file" | awk '{print $1 "  " FILENAME}' FILENAME="$(basename "$file")" > "$sha_file"
    elif command -v sha256sum &> /dev/null; then
        sha256sum "$file" | awk '{print $1 "  " FILENAME}' FILENAME="$(basename "$file")" > "$sha_file"
    else
        error "Neither shasum nor sha256sum found"
    fi

    info "Generated SHA256 for $(basename "$file")"
}

create_zip_package() {
    local name="$1"
    local binary_path="$RELEASE_DIR/$name"
    local zip_path="$RELEASE_DIR/$name.zip"

    # Handle Windows executables
    if [[ -f "$binary_path.exe" ]]; then
        binary_path="$binary_path.exe"
        name="$name.exe"
    fi

    if [[ ! -f "$binary_path" ]]; then
        warn "Binary not found: $binary_path (skipping)"
        return 1
    fi

    info "Creating package for $name..."

    # Generate SHA256 if not exists
    if [[ ! -f "$binary_path.sha256" ]]; then
        generate_sha256 "$binary_path"
    fi

    # Create temporary directory for packaging
    local temp_dir=$(mktemp -d)
    local binary_name=$(basename "$binary_path")

    # Copy files to temp directory
    cp "$binary_path" "$temp_dir/$binary_name"
    cp "$binary_path.sha256" "$temp_dir/$binary_name.sha256"

    # Add README to the package
    cat > "$temp_dir/README.txt" << EOF
Novus Compiler - $VERSION
==========================

This package contains:
- $binary_name        The Novus compiler binary
- $binary_name.sha256 SHA256 checksum for verification

Verification:
  macOS/Linux: shasum -a 256 -c $binary_name.sha256
  Windows:     certutil -hashfile $binary_name SHA256

Installation:
  1. Extract to a directory of your choice
  2. Add to your PATH or use full path to invoke
  3. Run: novus --version

Documentation: https://github.com/barryw/Novus
EOF

    # Create zip (remove old one first)
    rm -f "$zip_path"

    # Use zip command
    cd "$temp_dir"
    zip -q "$zip_path" "$binary_name" "$binary_name.sha256" "README.txt"
    cd - > /dev/null

    # Clean up
    rm -rf "$temp_dir"

    # Generate SHA256 for the zip itself
    generate_sha256 "$zip_path"

    # Show package info
    local zip_size=$(ls -lh "$zip_path" | awk '{print $5}')
    info "Created $zip_path ($zip_size)"

    return 0
}

upload_to_github() {
    info "Uploading to GitHub release $VERSION..."

    # Check if gh is installed
    if ! command -v gh &> /dev/null; then
        error "GitHub CLI (gh) is not installed"
    fi

    # Check if release exists
    if ! gh release view "$VERSION" &> /dev/null; then
        info "Creating release $VERSION..."
        gh release create "$VERSION" \
            --title "Novus $VERSION" \
            --notes "Release $VERSION" \
            --draft
    fi

    # Upload zip files and their SHA256s
    local upload_files=()
    for target in "${TARGETS[@]}"; do
        local name="${target%%:*}"
        local zip_path="$RELEASE_DIR/$name.zip"
        local sha_path="$RELEASE_DIR/$name.zip.sha256"

        if [[ -f "$zip_path" ]]; then
            upload_files+=("$zip_path")
        fi
        if [[ -f "$sha_path" ]]; then
            upload_files+=("$sha_path")
        fi
    done

    if [[ ${#upload_files[@]} -eq 0 ]]; then
        error "No files to upload"
    fi

    info "Uploading ${#upload_files[@]} files..."
    gh release upload "$VERSION" "${upload_files[@]}" --clobber

    info "Upload complete!"
    echo ""
    echo "Release URL: https://github.com/barryw/Novus/releases/tag/$VERSION"
}

# Main workflow
main() {
    info "Release directory: $RELEASE_DIR"

    if [[ "$BUILD" == true ]]; then
        info "Building binaries for all platforms..."

        for target in "${TARGETS[@]}"; do
            local name="${target%%:*}"
            local rid="${target##*:}"

            # Only build for current platform (cross-compilation requires setup)
            case "$rid" in
                osx-arm64)
                    if [[ "$(uname -s)" == "Darwin" && "$(uname -m)" == "arm64" ]]; then
                        build_binary "$name" "$rid"
                    else
                        warn "Skipping $name (not on macOS ARM64)"
                    fi
                    ;;
                osx-x64)
                    if [[ "$(uname -s)" == "Darwin" ]]; then
                        build_binary "$name" "$rid"
                    else
                        warn "Skipping $name (not on macOS)"
                    fi
                    ;;
                linux-x64)
                    if [[ "$(uname -s)" == "Linux" && "$(uname -m)" == "x86_64" ]]; then
                        build_binary "$name" "$rid"
                    else
                        warn "Skipping $name (not on Linux x64)"
                    fi
                    ;;
                linux-arm64)
                    if [[ "$(uname -s)" == "Linux" && "$(uname -m)" == "aarch64" ]]; then
                        build_binary "$name" "$rid"
                    else
                        warn "Skipping $name (not on Linux ARM64)"
                    fi
                    ;;
                win-x64)
                    if [[ "$(uname -s)" == MINGW* || "$(uname -s)" == CYGWIN* ]]; then
                        build_binary "$name" "$rid"
                    else
                        warn "Skipping $name (not on Windows)"
                    fi
                    ;;
            esac
        done
    fi

    # Create packages
    info "Creating zip packages..."
    local packaged=0

    for target in "${TARGETS[@]}"; do
        local name="${target%%:*}"
        if create_zip_package "$name"; then
            ((packaged++))
        fi
    done

    if [[ $packaged -eq 0 ]]; then
        error "No binaries found to package. Run with --build first."
    fi

    info "Packaged $packaged binaries"

    # Upload if requested
    if [[ "$UPLOAD" == true ]]; then
        upload_to_github
    fi

    # Summary
    echo ""
    info "Release preparation complete!"
    echo ""
    echo "Packages created in: $RELEASE_DIR"
    ls -lh "$RELEASE_DIR"/*.zip 2>/dev/null || true
    echo ""

    if [[ "$UPLOAD" != true ]]; then
        echo "To upload to GitHub:"
        echo "  $0 $VERSION --upload"
        echo ""
        echo "Or manually:"
        echo "  gh release upload $VERSION $RELEASE_DIR/*.zip $RELEASE_DIR/*.zip.sha256"
    fi
}

main
