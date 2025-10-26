@echo off
setlocal enabledelayedexpansion

set "OUTPUT_FILE=MergedCode_PureCMD.cs"
set "SEPARATOR=_------------------ "

REM 결과 파일을 초기화 (기존 내용 삭제)
> "%OUTPUT_FILE%" echo.
echo.
echo 📂 현재 폴더의 모든 .cs 파일을 합치는 중...

REM 현재 폴더의 모든 .cs 파일을 반복 처리
FOR %%f IN (*.cs) DO (
    REM 구분자 설정: _------------------ {FileName.cs} ---------------
    echo %SEPARATOR%%%f --------------- >> "%OUTPUT_FILE%"
    
    REM 파일 내용 합치기
    type "%%f" >> "%OUTPUT_FILE%"
    
    REM 가독성을 위한 빈 줄 추가 (선택 사항)
    echo. >> "%OUTPUT_FILE%"
    echo. >> "%OUTPUT_FILE%"
)

echo.
echo ✅ 작업이 완료되었습니다.
echo 결과 파일: %OUTPUT_FILE%
pause

endlocal