:: For Windows Platforms

:: Production mode
:: @echo off

:: Debugging mode
@echo on

:: Config places
SET TESTNET_PATH=hdd2\latestnet
SET NODE_BIN_PATH=src\Lachain.Console\bin\Debug\net5.0\win-x64\publish\Lachain.Console.exe
SET FAULTY=1
SET NODE_COUNT=4
SET BLOCK_TARGET=5000
SET CHAIN_ID=41
SET CYCLE_DURATION=40
SET VALIDATOR_COUNT=4
SET NETWORK=localnet

:: Remove old installation
DEL /S /Q %TESTNET_PATH%

:: Create testnet directory
IF NOT EXIST %TESTNET_PATH% MKDIR %TESTNET_PATH%

SET INDEX=0
:LOOP1
SET /A INDEX=%INDEX%+1
IF NOT EXIST "%TESTNET_PATH%\node_0%INDEX%\ChainLachain" MKDIR "%TESTNET_PATH%\node_0%INDEX%\ChainLachain"
IF %INDEX% NEQ %NODE_COUNT% GOTO LOOP1

:: Generate configs in testnet directory
COPY %NODE_BIN_PATH% %TESTNET_PATH%\Lachain.Console.exe
COPY %NODE_BIN_PATH% %TESTNET_PATH%\Lachain.Console.exe
CD %TESTNET_PATH%

Lachain.Console keygen --n %NODE_COUNT% --faulty %FAULTY% --p 7070 --t %BLOCK_TARGET% --c %CHAIN_ID% --k %NETWORK% --d %CYCLE_DURATION% --v %VALIDATOR_COUNT% --r 0 0 0 >> keys.txt

:: Go back to project main folder.
:: TESTNET_PATH has two paths, so need to do cd command twice below.
CD ..
CD ..

SET INDEX=0
:LOOP2
SET /A INDEX=%INDEX%+1
IF EXIST %TESTNET_PATH%\config0%INDEX%.json MOVE %TESTNET_PATH%\config0%INDEX%.json %TESTNET_PATH%\node_0%INDEX%\config.json
IF EXIST %TESTNET_PATH%\wallet0%INDEX%.json MOVE %TESTNET_PATH%\wallet0%INDEX%.json %TESTNET_PATH%\node_0%INDEX%\wallet.json
IF EXIST %TESTNET_PATH%\Lachain.Console.exe COPY %TESTNET_PATH%\Lachain.Console.exe %TESTNET_PATH%\node_0%INDEX%\Lachain.Console.exe
IF %INDEX% NEQ %NODE_COUNT% GOTO LOOP2

IF EXIST %TESTNET_PATH%\logs DEL /S /Q %TESTNET_PATH%\logs
:: IF EXIST %TESTNET_PATH%\Lachain.Console.exe DEL /S /Q %TESTNET_PATH%\Lachain.Console.exe