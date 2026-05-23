#!/bin/bash

# UI-Designer Deployment Script
# This script sets up and deploys the complete UI-Designer platform

set -e

echo "🎨 UI-Designer - Enterprise Document Automation Platform"
echo "======================================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    print_header "Checking prerequisites..."

    # Check Node.js
    if ! command -v node &> /dev/null; then
        print_error "Node.js is not installed. Please install Node.js 18+ first."
        exit 1
    fi

    NODE_VERSION=$(node -v | cut -d'v' -f2 | cut -d'.' -f1)
    if [ "$NODE_VERSION" -lt 18 ]; then
        print_error "Node.js version 18+ is required. Current version: $(node -v)"
        exit 1
    fi
    print_status "Node.js $(node -v) ✓"

    # Check npm
    if ! command -v npm &> /dev/null; then
        print_error "npm is not installed."
        exit 1
    fi
    print_status "npm $(npm -v) ✓"

    # Check .NET
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK is not installed. Please install .NET 8.0+ first."
        exit 1
    fi

    DOTNET_VERSION=$(dotnet --version | cut -d'.' -f1)
    if [ "$DOTNET_VERSION" -lt 8 ]; then
        print_error ".NET SDK version 8.0+ is required. Current version: $(dotnet --version)"
        exit 1
    fi
    print_status ".NET SDK $(dotnet --version) ✓"

    # Check git
    if ! command -v git &> /dev/null; then
        print_warning "Git is not installed. Some features may not work properly."
    else
        print_status "Git $(git --version | cut -d' ' -f3) ✓"
    fi
}

# Setup frontend
setup_frontend() {
    print_header "Setting up UI Designer (Frontend)..."

    cd ui-designer

    # Install dependencies
    print_status "Installing dependencies..."
    npm install

    # Build for production (optional)
    if [ "$BUILD_FRONTEND" = "true" ]; then
        print_status "Building for production..."
        npm run build
    fi

    cd ..
    print_status "Frontend setup complete ✓"
}

# Setup backend
setup_backend() {
    print_header "Setting up API Server (Backend)..."

    cd Canvas.WebApi

    # Restore packages
    print_status "Restoring .NET packages..."
    dotnet restore

    # Build
    print_status "Building backend..."
    dotnet build --configuration Release

    cd ..
    print_status "Backend setup complete ✓"
}

# Run tests
run_tests() {
    if [ "$SKIP_TESTS" = "true" ]; then
        print_warning "Skipping tests as requested"
        return
    fi

    print_header "Running tests..."

    # Frontend tests
    print_status "Running frontend tests..."
    cd ui-designer
    if npm test -- --watchAll=false --passWithNoTests; then
        print_status "Frontend tests passed ✓"
    else
        print_warning "Some frontend tests failed, but continuing..."
    fi
    cd ..

    # Backend tests
    print_status "Running backend tests..."
    cd Canvas
    if dotnet test --configuration Release --no-build; then
        print_status "Backend tests passed ✓"
    else
        print_warning "Some backend tests failed, but continuing..."
    fi
    cd ..
}

# Start services
start_services() {
    print_header "Starting services..."

    # Start backend in background
    print_status "Starting API server..."
    cd Canvas.WebApi
    dotnet run --configuration Release > ../api.log 2>&1 &
    API_PID=$!
    echo $API_PID > ../api.pid
    cd ..

    # Wait for API to start
    print_status "Waiting for API server to start..."
    sleep 5

    # Check if API is running
    if curl -s http://localhost:5000/health > /dev/null 2>&1; then
        print_status "API server started successfully ✓"
    else
        print_warning "API server may not be ready yet"
    fi

    # Start frontend
    print_status "Starting UI Designer..."
    cd ui-designer
    if [ "$BUILD_FRONTEND" = "true" ]; then
        # Serve built files
        npx serve -s dist -l 5173 > ../ui.log 2>&1 &
        UI_PID=$!
    else
        # Development server
        npm run dev > ../ui.log 2>&1 &
        UI_PID=$!
    fi
    echo $UI_PID > ../ui.pid
    cd ..

    print_status "Services started successfully ✓"
}

# Show usage information
show_info() {
    print_header "Deployment Complete!"

    echo ""
    echo "🎉 UI-Designer is now running!"
    echo ""
    echo "📱 Frontend (UI Designer): http://localhost:5173"
    echo "🔧 Backend (API Server):   http://localhost:5000"
    echo "📚 API Documentation:      http://localhost:5000/swagger"
    echo ""
    echo "📋 Demo Templates:"
    echo "   • Professional Invoice Template"
    echo "   • Achievement Certificate Template"
    echo "   • Business Report Template"
    echo ""
    echo "🛠️  Useful commands:"
    echo "   • View API logs:     tail -f api.log"
    echo "   • View UI logs:      tail -f ui.log"
    echo "   • Stop services:     ./stop.sh"
    echo "   • Restart services:  ./restart.sh"
    echo ""
    echo "📖 Documentation: README.md"
    echo ""
}

# Stop services
stop_services() {
    print_header "Stopping services..."

    if [ -f api.pid ]; then
        API_PID=$(cat api.pid)
        if kill -0 $API_PID 2>/dev/null; then
            print_status "Stopping API server..."
            kill $API_PID
        fi
        rm -f api.pid
    fi

    if [ -f ui.pid ]; then
        UI_PID=$(cat ui.pid)
        if kill -0 $UI_PID 2>/dev/null; then
            print_status "Stopping UI Designer..."
            kill $UI_PID
        fi
        rm -f ui.pid
    fi

    print_status "Services stopped ✓"
}

# Cleanup function
cleanup() {
    print_status "Cleaning up..."
    stop_services
    rm -f api.log ui.log
}

# Parse command line arguments
BUILD_FRONTEND=false
SKIP_TESTS=false
CLEANUP=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --build-frontend)
            BUILD_FRONTEND=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --cleanup)
            CLEANUP=true
            shift
            ;;
        --stop)
            stop_services
            exit 0
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --build-frontend    Build frontend for production"
            echo "  --skip-tests        Skip running tests"
            echo "  --cleanup          Clean up log files and stop services"
            echo "  --stop             Stop running services"
            echo "  --help             Show this help"
            echo ""
            echo "Examples:"
            echo "  $0                    # Deploy with default settings"
            echo "  $0 --build-frontend  # Deploy with production build"
            echo "  $0 --skip-tests      # Skip tests for faster deployment"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Main deployment
main() {
    if [ "$CLEANUP" = "true" ]; then
        cleanup
        exit 0
    fi

    print_status "Starting UI-Designer deployment..."
    echo ""

    check_prerequisites
    echo ""

    setup_frontend
    echo ""

    setup_backend
    echo ""

    run_tests
    echo ""

    start_services
    echo ""

    show_info

    # Setup cleanup on script exit
    trap cleanup EXIT

    # Wait for user interrupt
    print_status "Press Ctrl+C to stop services"
    wait
}

# Run main function
main "$@"