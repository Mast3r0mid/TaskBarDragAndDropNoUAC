# Change Log

All notable changes to this project will be documented in this file.

## Version 1.1.0 23/10/2023
# New Feature
- **Logging**: Integrated Serilog as the main event logger for comprehensive event tracking and management.
# Changes
- **Changes**: 
  -See previous beta release changelogs for details on changes and fixes introduced during the beta testing phase.
  

## Version 1.0.7-beta 21/10/2023
- **Changes**: 
  - again another search pattern.
  - change timers to hooks and windows msgs(removed MouseHook interval from settings).
  - now you can see and save some logs.
- **Fix**
  - Fixed various issues to boost application stability and reliability specially High resource usage and CPU Temp.
  - more localized windows compatibility.
  - many bug fixes.

## Version  1.0.6-beta 18/10/2023
- **Changes**: 
 -Modified search pattern (Basic Need for Localized language Windows users).
 -Bug Fix.

## Version  1.0.5-beta 18/10/2023
- **Changes**: 
 -Modified the 'Main Icon Invoke' function.
 -Removed the Win+T procedure in favor of relative Windows procedure.
 -Improved autoClick behavior to align with standard Windows behavior. 
 -Introduced automatic opening of pinned apps (customizable and you may need to enable this setting).


## Version  1.0.4-beta 17/10/2023
- **Changes**: 
  - New Taskbar Element Identification function .
  - Improved Element Interaction: Enhanced "invoke" and "focus" functions for quicker and more accurate UI element handling.
  - Enhanced Speed( Decreased delays, providing a faster and smoother user experience).
- **Fix**
  - Fixed various issues to boost application stability and reliability
  
 
## Version  1.0.3-beta 15/10/2023
- **Fix**: 
  - Resolve an issue that prevents the continuation of a drag action when the first taskbar item is invoked.

## Version Beta Good First issue 1.0.2-beta 15/10/2023
- **Fix**: 
  - Resolved an issue where an 'Invalid Rect Area' was returned for the taskbar.
  
  
  
## Version 1.0.1 14/10/2023

- **Fix**: 
  - Resolved an issue where icons containing "Window:" were not properly identified While TaskBar Mode is "Never Combine Items".
  

## Version 1.0.0 (Initial Release) 13/10/2023

- **Feature**: Enable Drag & Drop functionality for TaskBar in Windows 11 without requiring UAC or changing to classic mode and for Administrator Account.
- **Tested**: Successfully tested on Windows 11 22H2 (OS Build: 22621.2428).
- **Support**: Supports multiple screens.