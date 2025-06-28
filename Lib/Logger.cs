using Lib.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NodaTime;

namespace Lib
{
    // todo: replace logging with a widely-used package
    public class Logger(LogLevel logLevel, string filePath)
    {
        private LogLevel _logLevel = logLevel;
        private string _filePath = filePath;
        const int _lineWidth = 80;

        public string FormatCurrencyDisplay(string label, decimal value)
        {
            string stringValue = $"{value:C}";
            return FormatLabelAndDisplay(label, stringValue);
        }
        public string FormatDateDisplay(string label, LocalDateTime value)
        {
            string stringValue = $"{value:yyyy-MM-dd}";
            return FormatLabelAndDisplay(label, stringValue);
        }
        public string FormatRateDisplay(string label, decimal value)
        {
            string stringValue = $"{value:0.0000}";
            return FormatLabelAndDisplay(label, stringValue);
        }
        public string FormatBoolDisplay(string label, bool value)
        {
            string stringValue = value ? "Yes" : "No";
            return FormatLabelAndDisplay(label, stringValue);
        }
        public string FormatTimespanDisplay(string label, TimeSpan value)
        {
            string stringValue = $"{value.ToString("h'h 'm'm 's's'")}";
            return FormatLabelAndDisplay(label, stringValue);
        }
        public string FormatLabelAndDisplay(string label, string value)
        {
            int totalLength = _lineWidth;
            int labelLen = label.Length;
            int valueLen = value.Length;
            int whiteSpaceLen = totalLength - labelLen - valueLen;
            whiteSpaceLen = Math.Max(whiteSpaceLen, 3); // we'll over-run total length rather than lose info
            return $"{label}{new String(' ', whiteSpaceLen)}{value}";
        }
        public string FormatHeading(string label)
        {
            int totalLength = _lineWidth;
            int labelLen = label.Length;
            int whiteSpaceLen = totalLength - labelLen;
            int leftHalfWhiteSpace = (int)Math.Round(whiteSpaceLen * 0.5, 0);
            int rightHalfWhiteSpace = totalLength - labelLen - leftHalfWhiteSpace;
            leftHalfWhiteSpace = Math.Max(leftHalfWhiteSpace, 3); // we'll over-run total length rather than lose info
            rightHalfWhiteSpace = Math.Max(rightHalfWhiteSpace, 3);
            return $"{new String(' ', leftHalfWhiteSpace)}{label}{new String(' ', rightHalfWhiteSpace)}";
        }
        public string FormatHighlightHeading(string label, char highlightBarChar)
        {
            int highlightBarLength = 16;
            int bufferLength = 8;
            string heading =
                new String(highlightBarChar, highlightBarLength) +
                new String(' ', bufferLength) +
                label +
                new String(' ', bufferLength) +
                new String(highlightBarChar, highlightBarLength);
            return FormatHeading(heading);
        }
        public string FormatBarSeparator(char charToDisplay)
        {
            return new String(charToDisplay, _lineWidth);
        }

        public void Debug(string message)
        {
            if(_logLevel == LogLevel.DEBUG)
            {
                PrintLog(LogLevel.DEBUG, message);
            }
        }
        public void Info(string message)
        {
            if ((int)_logLevel <= (int)LogLevel.INFO)
            {
                PrintLog(LogLevel.INFO, message);
            }
        }
        public void Warn(string message)
        {
            if ((int)_logLevel <= (int)LogLevel.WARN)
            {
                PrintLog(LogLevel.WARN, message);
            }
        }
        public void Error(string message)
        {
            if ((int)_logLevel <= (int)LogLevel.ERROR)
            {
                PrintLog(LogLevel.ERROR, message);
            }
        }
        public void Fatal(string message)
        {
            if ((int)_logLevel <= (int)LogLevel.FATAL)
            {
                PrintLog(LogLevel.FATAL, message);
            }
        }
        private void PrintLog(LogLevel logLevel, string message)
        {
            string logEntry = $"{DateTime.Now}\t{logLevel}\t{message}";
            Console.WriteLine(logEntry);
            using (StreamWriter outputFile = new StreamWriter(_filePath, true))
            {
                outputFile.WriteLine(logEntry);
            }
        }
    }
}