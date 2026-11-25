#!/bin/bash
# Mechanical first pass: Windows (WinUI) source -> Avalonia source. Usage: port.sh src dst
sed -E \
 -e '/^using (Microsoft\.UI|Microsoft\.Windows|Windows\.)[A-Za-z.]*;/d' \
 -e 's/\(Brush\)Application\.Current\.Resources\[/Ui.Brush(/g' \
 -e 's/Application\.Current\.Resources\[("[A-Za-z]+")\]/Ui.Brush(\1)/g' \
 -e 's/Ui\.Brush\(("[A-Za-z]+"|[^]]+)\]/Ui.Brush(\1)/g' \
 -e 's/Microsoft\.UI\.Text\.FontWeights/FontWeights/g' \
 -e 's/ToolTipService\.SetToolTip\(/Ui.SetTip(/g' \
 -e 's/AutomationProperties\.SetName\(/Ui.Name(/g' \
 -e 's/\.Visibility = Visibility\.Collapsed/.IsVisible = false/g' \
 -e 's/\.Visibility = Visibility\.Visible/.IsVisible = true/g' \
 -e 's/Visibility = Visibility\.Collapsed/IsVisible = false/g' \
 -e 's/Visibility = Visibility\.Visible/IsVisible = true/g' \
 -e 's/ : Page$/ : PageBase/' \
 -e 's/RoutedEventArgs/Avalonia.Interactivity.RoutedEventArgs/g' \
 "$1" > "$2"
