
@echo off
sc create "VaeLldpService" binPath= "%cd%\LLDP.Service.Windows.exe" start= auto
npcap-1.60.exe
sc start "VaeLldpService"