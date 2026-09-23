#!/bin/bash
# SkiaSharp (used by ImageProcessingService for photo uploads) needs libfontconfig.so.1 at
# runtime; Amazon Linux 2023's EB base AMI doesn't include it, so every photo upload fails
# with a DllNotFoundException that PhotoUploadService maps to "Photos must be JPEG, PNG, or
# WEBP images." regardless of the actual file. Install it before the app starts.
dnf install -y fontconfig
