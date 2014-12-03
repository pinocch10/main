using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Excel;
using log4net.Appender;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MovieSchedule.Core.Extensions;
using MovieSchedule.Data;
using MovieSchedule.Data.Helpers;
using MovieSchedule.Parsers.Common;
using MovieSchedule.Parsers.Kinopoisk;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace MovieSchedule.Tests.Data
{
    [TestClass]
    public class DataTests
    {

        private const string MasterListSource = @"C:\master-list-source.txt";

        [TestMethod]
        public void HtmlDecode()
        {
            var sourceString =
                "ctl00%24ctl08=ctl00%24contentPlaceHolder%24UpdatePanel1%7Cctl00%24contentPlaceHolder%24drdDay&__EVENTTARGET=ctl00%24contentPlaceHolder%24drdDay&__EVENTARGUMENT=&__LASTFOCUS=&__VIEWSTATE=%2FwEPDwUJMTEzOTM2NTAwD2QWAmYPDxYIHglNZXRhVGl0bGUFNsKr0JvRjtC60YHQvtGAINCm0LXQvdGC0YDCuyAtINCg0LXQv9C10YDRgtGD0LDRgCDQvdCwIB4Ia2V5d29yZHMFNsKr0JvRjtC60YHQvtGAINCm0LXQvdGC0YDCuyAtINCg0LXQv9C10YDRgtGD0LDRgCDQvdCwIB4LZGVzY3JpcHRpb24FNsKr0JvRjtC60YHQvtGAINCm0LXQvdGC0YDCuyAtINCg0LXQv9C10YDRgtGD0LDRgCDQvdCwIB4LYnJlYWRjcnVtYnNlZBYCAgQPZBYWAgEPFgIeCWlubmVyaHRtbAUCMTBkAgIPFgIfBAUBMWQCAw8WAh8EBQMzNztkAgQPFgIfBAUURWxraV9iYWNrZ3JvdW5kLmpwZztkAgUPFgIfBAUURWxraV9iYWNrZ3JvdW5kLmpwZztkAgYPFgIfBAUGMS5wbmc7ZAIHDxYCHwQFATtkAggPFgIfBAUFOzA7MDtkAgkPFgIfBAUFOzA7MDtkAgoPFgIfBAUFOzA7MDtkAgwQZGQWAgIBD2QWDgICD2QWAgIBD2QWAgIBD2QWAmYPFgIeB1Zpc2libGVoZAIDDxYCHgRUZXh0BV08ZGl2IGNsYXNzPSJicmFuZGluZyI%2BPGEgaHJlZj0iaHR0cDovL3d3dy5sdXhvcmZpbG0ucnUvRmVhdHVyZXMvQmFubmVyLmFzaHg%2FaWQ9MTgiPjwvYT48L2Rpdj5kAgQPDxYCHg1CYW5uZXJHcm91cElkAgFkFgJmDzwrAAkBAA8WBB4IRGF0YUtleXMWAB4LXyFJdGVtQ291bnQCBGQWCGYPZBYCZg8VAfACPGEgY2xhc3M9J251bWVyQ2xhc3MwJyBocmVmPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9GZWF0dXJlcy9CYW5uZXIuYXNoeD9pZD0xNCcgIG9ubW91c2VvdmVyPSJ3aW5kb3cuc3RhdHVzPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9uZXdzL25ld3M2LTE0OTguYXNweCc7IHJldHVybiB0cnVlOyIgb25tb3VzZWVudGVyPSJ3aW5kb3cuc3RhdHVzPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9uZXdzL25ld3M2LTE0OTguYXNweCc7IHJldHVybiB0cnVlOyIgb25tb3VzZW91dD0id2luZG93LnN0YXR1cz0nJyI%2BPGltZyBzcmM9J2h0dHA6Ly93d3cubHV4b3JmaWxtLnJ1L1VwbG9hZC9CYW5uZXJzLzE1LnBuZycgICBhbHQ9J29jdF8xJyAvPjwvYT5kAgEPZBYCZg8VAYIDPGEgY2xhc3M9J251bWVyQ2xhc3MxJyBocmVmPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9GZWF0dXJlcy9CYW5uZXIuYXNoeD9pZD0xNScgIG9ubW91c2VvdmVyPSJ3aW5kb3cuc3RhdHVzPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9uZXdzL25ld3M2LTE0NzguYXNweCc7IHJldHVybiB0cnVlOyIgb25tb3VzZWVudGVyPSJ3aW5kb3cuc3RhdHVzPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9uZXdzL25ld3M2LTE0NzguYXNweCc7IHJldHVybiB0cnVlOyIgb25tb3VzZW91dD0id2luZG93LnN0YXR1cz0nJyI%2BPGltZyBzcmM9J2h0dHA6Ly93d3cubHV4b3JmaWxtLnJ1L1VwbG9hZC9CYW5uZXJzLzY3MF8zMjBfMl9wb18xOTQyMjIyLnBuZycgICBhbHQ9J29jdF8yJyAvPjwvYT5kAgIPZBYCZg8VAZwDPGEgY2xhc3M9J251bWVyQ2xhc3MyJyBocmVmPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9GZWF0dXJlcy9CYW5uZXIuYXNoeD9pZD0xNicgIG9ubW91c2VvdmVyPSJ3aW5kb3cuc3RhdHVzPSdodHRwczovL2l0dW5lcy5hcHBsZS5jb20vcnUvYXBwL2tpbm90ZWF0cnktbHVrc29yL2lkNzE0ODU0OTM5JzsgcmV0dXJuIHRydWU7IiBvbm1vdXNlZW50ZXI9IndpbmRvdy5zdGF0dXM9J2h0dHBzOi8vaXR1bmVzLmFwcGxlLmNvbS9ydS9hcHAva2lub3RlYXRyeS1sdWtzb3IvaWQ3MTQ4NTQ5MzknOyByZXR1cm4gdHJ1ZTsiIG9ubW91c2VvdXQ9IndpbmRvdy5zdGF0dXM9JyciPjxpbWcgc3JjPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9VcGxvYWQvQmFubmVycy9hcHBsZV9iYW5uZXIuUE5HJyAgIGFsdD0nb2N0XzMnIC8%2BPC9hPmQCAw9kFgJmDxUBqQM8YSBjbGFzcz0nbnVtZXJDbGFzczMnIGhyZWY9J2h0dHA6Ly93d3cubHV4b3JmaWxtLnJ1L0ZlYXR1cmVzL0Jhbm5lci5hc2h4P2lkPTE3JyAgb25tb3VzZW92ZXI9IndpbmRvdy5zdGF0dXM9J2h0dHBzOi8vcGxheS5nb29nbGUuY29tL3N0b3JlL2FwcHMvZGV0YWlscz9pZD1ydS5kcml2ZXBpeGVscy5sdXhvcic7IHJldHVybiB0cnVlOyIgb25tb3VzZWVudGVyPSJ3aW5kb3cuc3RhdHVzPSdodHRwczovL3BsYXkuZ29vZ2xlLmNvbS9zdG9yZS9hcHBzL2RldGFpbHM%2FaWQ9cnUuZHJpdmVwaXhlbHMubHV4b3InOyByZXR1cm4gdHJ1ZTsiIG9ubW91c2VvdXQ9IndpbmRvdy5zdGF0dXM9JyciPjxpbWcgc3JjPSdodHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS9VcGxvYWQvQmFubmVycy9hbmRyb2lkX2RsX25ldzMucG5nJyAgIGFsdD0nb2N0XzQnIC8%2BPC9hPmQCBQ9kFgICAQ9kFghmD2QWAmYPZBYCAgEPFCsAAg8WBB8GBQzQnNC%2B0YHQutCy0LAeE2NhY2hlZFNlbGVjdGVkVmFsdWVkZA8UKwAOFCsAAg8WBh8GBQzQnNC%2B0YHQutCy0LAeBVZhbHVlBQExHghTZWxlY3RlZGdkZBQrAAIPFgYfBgUd0KHQsNC90LrRgi3Qn9C10YLQtdGA0LHRg9GA0LMfCwUCMTQfDGhkZBQrAAIPFgYfBgUQ0JHQsNC70LDRiNC40YXQsB8LBQE0HwxoZGQUKwACDxYGHwYFDNCR0YDRj9C90YHQuh8LBQIxNh8MaGRkFCsAAg8WBh8GBQ7QktC%2B0YDQvtC90LXQth8LBQIxMB8MaGRkFCsAAg8WBh8GBRbQktC%2B0YHQutGA0LXRgdC10L3RgdC6HwsFATUfDGhkZBQrAAIPFgYfBgUS0JbRg9C60L7QstGB0LrQuNC5HwsFATgfDGhkZBQrAAIPFgYfBgUI0JrQu9C40L0fCwUBMx8MaGRkFCsAAg8WBh8GBQrQmtGD0YDRgdC6HwsFAjE1HwxoZGQUKwACDxYGHwYFGdCe0YDQtdGF0L7QstC%2BLdCX0YPQtdCy0L4fCwUBNx8MaGRkFCsAAg8WBh8GBRrQoNC%2B0YHRgtC%2B0LIt0L3QsC3QlNC%2B0L3Rgx8LBQIxMR8MaGRkFCsAAg8WBh8GBQzQoNGP0LfQsNC90YwfCwUBOR8MaGRkFCsAAg8WBh8GBRnQodC10YDQs9C40LXQsiDQn9C%2B0YHQsNC0HwsFATYfDGhkZBQrAAIPFgYfBgUI0KHQvtGH0LgfCwUCMTIfDGhkZA8UKwEOZmZmZmZmZmZmZmZmZmYWAQV3VGVsZXJpay5XZWIuVUkuUmFkQ29tYm9Cb3hJdGVtLCBUZWxlcmlrLldlYi5VSSwgVmVyc2lvbj0yMDExLjEuMzE1LjQwLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPTEyMWZhZTc4MTY1YmEzZDQWIGYPDxYEHghDc3NDbGFzcwUJcmNiSGVhZGVyHgRfIVNCAgJkZAIBDw8WBB8NBQlyY2JGb290ZXIfDgICZGQCAg8PFgYfBgUM0JzQvtGB0LrQstCwHwsFATEfDGdkZAIDDw8WBh8GBR3QodCw0L3QutGCLdCf0LXRgtC10YDQsdGD0YDQsx8LBQIxNB8MaGRkAgQPDxYGHwYFENCR0LDQu9Cw0YjQuNGF0LAfCwUBNB8MaGRkAgUPDxYGHwYFDNCR0YDRj9C90YHQuh8LBQIxNh8MaGRkAgYPDxYGHwYFDtCS0L7RgNC%2B0L3QtdC2HwsFAjEwHwxoZGQCBw8PFgYfBgUW0JLQvtGB0LrRgNC10YHQtdC90YHQuh8LBQE1HwxoZGQCCA8PFgYfBgUS0JbRg9C60L7QstGB0LrQuNC5HwsFATgfDGhkZAIJDw8WBh8GBQjQmtC70LjQvR8LBQEzHwxoZGQCCg8PFgYfBgUK0JrRg9GA0YHQuh8LBQIxNR8MaGRkAgsPDxYGHwYFGdCe0YDQtdGF0L7QstC%2BLdCX0YPQtdCy0L4fCwUBNx8MaGRkAgwPDxYGHwYFGtCg0L7RgdGC0L7Qsi3QvdCwLdCU0L7QvdGDHwsFAjExHwxoZGQCDQ8PFgYfBgUM0KDRj9C30LDQvdGMHwsFATkfDGhkZAIODw8WBh8GBRnQodC10YDQs9C40LXQsiDQn9C%2B0YHQsNC0HwsFATYfDGhkZAIPDw8WBh8GBQjQodC%2B0YfQuB8LBQIxMh8MaGRkAgEPZBYCZg9kFgICAQ8UKwACDxYEHwYFG9CS0YHQtSDQutC40L3QvtGC0LXQsNGC0YDRix8KZGQPFCsACBQrAAIPFgYfBgUb0JLRgdC1INC60LjQvdC%2B0YLQtdCw0YLRgNGLHwsFAi0xHwxnZGQUKwACDxYGHwYFG8Kr0JvRjtC60YHQvtGAINCm0LXQvdGC0YDCux8LBQI0MB8MaGRkFCsAAg8WBh8GBRvCq9Cb0Y7QutGB0L7RgCDQktC10YHQvdCwwrsfCwUCNDcfDGhkZBQrAAIPFgYfBgUdwqvQm9GO0LrRgdC%2B0YAg0JPRg9C00LfQvtC9wrsfCwUCNDYfDGhkZBQrAAIPFgYfBgUgwqvQm9GO0LrRgdC%2B0YDCuyDQsiDQotCg0JogVkVHQVMfCwUCMTEfDGhkZBQrAAIPFgYfBgUdwqvQm9GO0LrRgdC%2B0YDCuyDQnNC40YLQuNC90L4fCwUCMTUfDGhkZBQrAAIPFgYfBgUhwqvQm9GO0LrRgdC%2B0YDCuyDQntGC0YDQsNC00L3QvtC1HwsFAjEwHwxoZGQUKwACDxYGHwYFH8Kr0JvRjtC60YHQvtGAwrsg0K%2FRgdC10L3QtdCy0L4fCwUCMTMfDGhkZA8UKwEIZmZmZmZmZmYWAQV3VGVsZXJpay5XZWIuVUkuUmFkQ29tYm9Cb3hJdGVtLCBUZWxlcmlrLldlYi5VSSwgVmVyc2lvbj0yMDExLjEuMzE1LjQwLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPTEyMWZhZTc4MTY1YmEzZDQWFGYPDxYEHw0FCXJjYkhlYWRlch8OAgJkZAIBDw8WBB8NBQlyY2JGb290ZXIfDgICZGQCAg8PFgYfBgUb0JLRgdC1INC60LjQvdC%2B0YLQtdCw0YLRgNGLHwsFAi0xHwxnZGQCAw8PFgYfBgUbwqvQm9GO0LrRgdC%2B0YAg0KbQtdC90YLRgMK7HwsFAjQwHwxoZGQCBA8PFgYfBgUbwqvQm9GO0LrRgdC%2B0YAg0JLQtdGB0L3QsMK7HwsFAjQ3HwxoZGQCBQ8PFgYfBgUdwqvQm9GO0LrRgdC%2B0YAg0JPRg9C00LfQvtC9wrsfCwUCNDYfDGhkZAIGDw8WBh8GBSDCq9Cb0Y7QutGB0L7RgMK7INCyINCi0KDQmiBWRUdBUx8LBQIxMR8MaGRkAgcPDxYGHwYFHcKr0JvRjtC60YHQvtGAwrsg0JzQuNGC0LjQvdC%2BHwsFAjE1HwxoZGQCCA8PFgYfBgUhwqvQm9GO0LrRgdC%2B0YDCuyDQntGC0YDQsNC00L3QvtC1HwsFAjEwHwxoZGQCCQ8PFgYfBgUfwqvQm9GO0LrRgdC%2B0YDCuyDQr9GB0LXQvdC10LLQvh8LBQIxMx8MaGRkAgIPZBYCZg9kFgICAQ8UKwACZGRkAgMPPCsABABkAggPZBYCZg9kFgICAQ8WAh8FaGQCCQ9kFgQCAQ9kFgJmD2QWAmYPEGRkFgBkAgUPZBYCAgUPZBYCAgEPFgIfCQIDFgZmD2QWAmYPFQIsaHR0cDovL3d3dy5sdXhvcmZpbG0ucnUvbmV3cy9uZXdzNi0xNDk4LmFzcHhKL3VwbG9hZC9DaW5lbWFCYW5uZXJzLzQwLzI1Nl81MTFhc2JhMjhhYTFlLTA5ODYtNGYxZS05MzA4LTU1M2U3NmU3ZmFmZC5qcGdkAgEPZBYCZg8VAi5odHRwOi8vd3d3Lmx1eG9yZmlsbS5ydS92YWNhbmNpZXMvZGVmYXVsdC5hc3B4hQEvdXBsb2FkL0NpbmVtYUJhbm5lcnMvNDAvMzZmOS1mMzZjLTQ3MGQtODdkOS1mMGEzZWNhZjI5MWZhMzM0OWY0YS00Mzk5LTRhODMtYjc1MS01YTIxNjBjZTZjNDgwZWYyMWMzNS05MzA4LTQ3NDItYmRlMy02NzJlYWMzM2E1OGMuanBnZAICD2QWAmYPFQIAdi91cGxvYWQvQ2luZW1hQmFubmVycy80MC8yNTZfNTExX3Rha2VfYXdheWQ5MzBjN2U3LTc0YTAtNGJjZS05NTFhLTVlM2QzOWEyMWEyYmI0MTAxZjk3LWZhOTAtNGQ1NS1iZWUwLTZjMjFhODNmNGQxYy5qcGdkAgoPZBYCZg9kFgJmD2QWBgIBDxQrAAIPFgYeC18hRGF0YUJvdW5kZx8GBQzQnNC%2B0YHQutCy0LAfCmRkDxQrAA4UKwACDxYGHwYFDNCc0L7RgdC60LLQsB8LBQExHwxnZGQUKwACDxYGHwYFHdCh0LDQvdC60YIt0J%2FQtdGC0LXRgNCx0YPRgNCzHwsFAjE0HwxoZGQUKwACDxYGHwYFENCR0LDQu9Cw0YjQuNGF0LAfCwUBNB8MaGRkFCsAAg8WBh8GBQzQkdGA0Y%2FQvdGB0LofCwUCMTYfDGhkZBQrAAIPFgYfBgUO0JLQvtGA0L7QvdC10LYfCwUCMTAfDGhkZBQrAAIPFgYfBgUW0JLQvtGB0LrRgNC10YHQtdC90YHQuh8LBQE1HwxoZGQUKwACDxYGHwYFEtCW0YPQutC%2B0LLRgdC60LjQuR8LBQE4HwxoZGQUKwACDxYGHwYFCNCa0LvQuNC9HwsFATMfDGhkZBQrAAIPFgYfBgUK0JrRg9GA0YHQuh8LBQIxNR8MaGRkFCsAAg8WBh8GBRnQntGA0LXRhdC%2B0LLQvi3Ql9GD0LXQstC%2BHwsFATcfDGhkZBQrAAIPFgYfBgUa0KDQvtGB0YLQvtCyLdC90LAt0JTQvtC90YMfCwUCMTEfDGhkZBQrAAIPFgYfBgUM0KDRj9C30LDQvdGMHwsFATkfDGhkZBQrAAIPFgYfBgUZ0KHQtdGA0LPQuNC10LIg0J%2FQvtGB0LDQtB8LBQE2HwxoZGQUKwACDxYGHwYFCNCh0L7Rh9C4HwsFAjEyHwxoZGQPFCsBDmZmZmZmZmZmZmZmZmZmFgEFd1RlbGVyaWsuV2ViLlVJLlJhZENvbWJvQm94SXRlbSwgVGVsZXJpay5XZWIuVUksIFZlcnNpb249MjAxMS4xLjMxNS40MCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj0xMjFmYWU3ODE2NWJhM2Q0FiBmDw8WBB8NBQlyY2JIZWFkZXIfDgICZGQCAQ8PFgQfDQUJcmNiRm9vdGVyHw4CAmRkAgIPDxYGHwYFDNCc0L7RgdC60LLQsB8LBQExHwxnZGQCAw8PFgYfBgUd0KHQsNC90LrRgi3Qn9C10YLQtdGA0LHRg9GA0LMfCwUCMTQfDGhkZAIEDw8WBh8GBRDQkdCw0LvQsNGI0LjRhdCwHwsFATQfDGhkZAIFDw8WBh8GBQzQkdGA0Y%2FQvdGB0LofCwUCMTYfDGhkZAIGDw8WBh8GBQ7QktC%2B0YDQvtC90LXQth8LBQIxMB8MaGRkAgcPDxYGHwYFFtCS0L7RgdC60YDQtdGB0LXQvdGB0LofCwUBNR8MaGRkAggPDxYGHwYFEtCW0YPQutC%2B0LLRgdC60LjQuR8LBQE4HwxoZGQCCQ8PFgYfBgUI0JrQu9C40L0fCwUBMx8MaGRkAgoPDxYGHwYFCtCa0YPRgNGB0LofCwUCMTUfDGhkZAILDw8WBh8GBRnQntGA0LXRhdC%2B0LLQvi3Ql9GD0LXQstC%2BHwsFATcfDGhkZAIMDw8WBh8GBRrQoNC%2B0YHRgtC%2B0LIt0L3QsC3QlNC%2B0L3Rgx8LBQIxMR8MaGRkAg0PDxYGHwYFDNCg0Y%2FQt9Cw0L3RjB8LBQE5HwxoZGQCDg8PFgYfBgUZ0KHQtdGA0LPQuNC10LIg0J%2FQvtGB0LDQtB8LBQE2HwxoZGQCDw8PFgYfBgUI0KHQvtGH0LgfCwUCMTIfDGhkZAIDDxQrAAIPFgQfD2cfCmRkDxQrAAcUKwACDxYGHwYFG8Kr0JvRjtC60YHQvtGAINCm0LXQvdGC0YDCux8LBQI0MB8MZ2RkFCsAAg8WBh8GBRvCq9Cb0Y7QutGB0L7RgCDQktC10YHQvdCwwrsfCwUCNDcfDGhkZBQrAAIPFgYfBgUdwqvQm9GO0LrRgdC%2B0YAg0JPRg9C00LfQvtC9wrsfCwUCNDYfDGhkZBQrAAIPFgYfBgUgwqvQm9GO0LrRgdC%2B0YDCuyDQsiDQotCg0JogVkVHQVMfCwUCMTEfDGhkZBQrAAIPFgYfBgUdwqvQm9GO0LrRgdC%2B0YDCuyDQnNC40YLQuNC90L4fCwUCMTUfDGhkZBQrAAIPFgYfBgUhwqvQm9GO0LrRgdC%2B0YDCuyDQntGC0YDQsNC00L3QvtC1HwsFAjEwHwxoZGQUKwACDxYGHwYFH8Kr0JvRjtC60YHQvtGAwrsg0K%2FRgdC10L3QtdCy0L4fCwUCMTMfDGhkZA8UKwEHZmZmZmZmZhYBBXdUZWxlcmlrLldlYi5VSS5SYWRDb21ib0JveEl0ZW0sIFRlbGVyaWsuV2ViLlVJLCBWZXJzaW9uPTIwMTEuMS4zMTUuNDAsIEN1bHR1cmU9bmV1dHJhbCwgUHVibGljS2V5VG9rZW49MTIxZmFlNzgxNjViYTNkNBYSZg8PFgQfDQUJcmNiSGVhZGVyHw4CAmRkAgEPDxYEHw0FCXJjYkZvb3Rlch8OAgJkZAICDw8WBh8GBRvCq9Cb0Y7QutGB0L7RgCDQptC10L3RgtGAwrsfCwUCNDAfDGdkZAIDDw8WBh8GBRvCq9Cb0Y7QutGB0L7RgCDQktC10YHQvdCwwrsfCwUCNDcfDGhkZAIEDw8WBh8GBR3Cq9Cb0Y7QutGB0L7RgCDQk9GD0LTQt9C%2B0L3Cux8LBQI0Nh8MaGRkAgUPDxYGHwYFIMKr0JvRjtC60YHQvtGAwrsg0LIg0KLQoNCaIFZFR0FTHwsFAjExHwxoZGQCBg8PFgYfBgUdwqvQm9GO0LrRgdC%2B0YDCuyDQnNC40YLQuNC90L4fCwUCMTUfDGhkZAIHDw8WBh8GBSHCq9Cb0Y7QutGB0L7RgMK7INCe0YLRgNCw0LTQvdC%2B0LUfCwUCMTAfDGhkZAIIDw8WBh8GBR%2FCq9Cb0Y7QutGB0L7RgMK7INCv0YHQtdC90LXQstC%2BHwsFAjEzHwxoZGQCBQ8WAh8GBRXQvtGCIDMzMCDRgNGD0LHQu9C10LlkGAYFG2N0bDAwJHNlcmFjaFBBbmVsJGNvbWJvQ2l0eQ8UKwACBQzQnNC%2B0YHQutCy0LAFATFkBR1jdGwwMCRzZXJhY2hQQW5lbCRjb21ib0NpbmVtYQ8UKwACBRvQktGB0LUg0LrQuNC90L7RgtC10LDRgtGA0YsFAi0xZAUcY3RsMDAkc2VyYWNoUEFuZWwkY29tYm9Nb3ZpZQ8UKwACZWVkBRtjdGwwMCRjdGwxMCRjb21ib0NpdHlGb290ZXIPFCsAAgUM0JzQvtGB0LrQstCwBQExZAUdY3RsMDAkY3RsMTAkY29tYm9DaW5lbWFGb290ZXIPFCsAAmUFAjQwZAUYY3RsMDAkbG9naW5Cb3gkbXVsdGlWaWV3Dw9kZmSjCnIKSr1SxMpmnNW89jzr9X0kNkGYhyaEc1H9nyXCrw%3D%3D&__EVENTVALIDATION=%2FwEdAAbSPB%2B49%2Fr7QjKDBObhsx7hS9gaNd6S8UyRF94UOalVHcdhwjZP07KDFA6ZevIV1Noj4Po8LUlmeUzP2zJS5FzjpHfJBAFSzbwUn90mQL%2FWGBR8FJV1X2osB27M98FYqvwgDJcyr%2BffVg8DHdpzWQbKIurLniSI0XWW686Oo%2Fk1yw%3D%3D&ctl00%24contentPlaceHolder%24dateText=03.12.2014&ctl00%24contentPlaceHolder%24hdnSelectedDate=03.12.2014&ctl00%24contentPlaceHolder%24hdnEnableDates=02.12.2014%3B03.12.2014%3B04.12.2014%3B05.12.2014%3B06.12.2014%3B09.12.2014%3B16.12.2014%3B23.12.2014%3B30.12.2014&ctl00%24contentPlaceHolder%24hdnMaxDate=30.12.2014&__ASYNCPOST=true&";
            var decodedString = HttpUtility.UrlDecode(sourceString);

            Console.WriteLine(decodedString);
        }

        [TestMethod]
        public void TestUri()
        {
            var uri = new Uri("http://www.luxorfilm.ru/cinema/");
            var relativeUri = "?cityId=14";
            var baseRelativeUri = "/afisha/";
            Console.WriteLine(new Uri(uri, relativeUri).ToString());
            Console.WriteLine(new Uri(uri, baseRelativeUri).ToString());

            //Console.WriteLine(uri.MakeRelativeUri(new Uri(relativeUri)).ToString());
            //Console.WriteLine(uri.MakeRelativeUri(new Uri(baseRelativeUri)).ToString());
        }

        [TestMethod]
        public void TestSessiontimesComparer()
        {
            var list = new List<string>
            {
                 "00:55",
                 "11:20",
                 "16:00",
                 "20:50"
            };

            var newList = list.OrderBy(x => x, DataHelper.SessionsComparer).ToList();

            foreach (var s in newList)
            {
                Console.WriteLine(s);
            }

        }

        [TestMethod]
        public void PopulateSessions()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                for (int hour = 0; hour <= 23; hour++)
                {
                    for (int minute = 0; minute <= 55; minute += 5)
                    {
                        model.Sessions.Add(new Session()
                        {
                            Time = string.Format("{0:00}:{1:00}", hour, minute),
                            Hour = hour,
                            Minute = minute
                        });
                    }
                }
                model.SaveChanges();
            }
        }

        [TestMethod]
        public void SnapshotsTest()
        {
            var sw = new Stopwatch();
            sw.Start();
            int parseRunId = 633;
            ParsingHelper.PopulateSnapshots(parseRunId);
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
        }

        [TestMethod]
        public void KinpoiskSuggestionTest()
        {
            new KinopoiskParser().GetMovieListWithDeviation();
        }

        [TestMethod]
        public void LevenshteinTest()
        {
            string searchTerm = "Матч Пойнт";
            //string searchTerm = "Семейка Крудс 2";
            var model = new MovieScheduleStatsEntities();
            var result = model.Database.SqlQuery<MovieSearchResult>("SELECT Id, Title, OriginalTitle, ReleaseDate, dbo.fn_LevenshteinDistancePercentage(LOWER(REPLACE([Title],' ','')),LOWER(REPLACE(ISNULL(@searchTitle,''),' ',''))) AS Simmilarity FROM [dbo].[Movie] WHERE dbo.fn_LevenshteinDistancePercentage(LOWER(REPLACE([Title],' ','')),LOWER(REPLACE(ISNULL(@searchTitle,''),' ',''))) > 85", new SqlParameter("searchTitle", searchTerm)).ToList();
            var result2 = model.Database.SqlQuery<MovieSearchResult>("SELECT Id, Title, OriginalTitle, ReleaseDate, dbo.fn_LevenshteinDistance(LOWER(REPLACE([Title],' ','')),LOWER(REPLACE(ISNULL(@searchTitle,''),' ',''))) AS Simmilarity FROM [dbo].[Movie] WHERE dbo.fn_LevenshteinDistance(LOWER(REPLACE([Title],' ','')),LOWER(REPLACE(ISNULL(@searchTitle,''),' ',''))) > 4", new SqlParameter("searchTitle", searchTerm)).ToList();
            Console.WriteLine(result.Count());
            foreach (var item in result)
            {
                Console.WriteLine("{0} <{5}> <{1}> / {2} {3:yyyy-MM-dd} {4}%", item.Id, item.Title, item.OriginalTitle, item.ReleaseDate, item.Simmilarity, searchTerm);
            }

        }

        [TestMethod]
        public void RegexTest()
        {
            var tt = "Костино (Королёв)".ReplaceBadChar();
            var t = Regex.Replace("Киноград (Лесной городок)", string.Format("\\({0}\\)", "Лесной Городок"), string.Empty, RegexOptions.IgnoreCase);
            Console.WriteLine(t);
        }

        [TestMethod]
        public void CitySatelites()
        {
            using (var model = new MovieScheduleStatsEntities())
            {

                var country = ParsingHelper.GetCountry(model);
                var masterCity = new City
                    {
                        Name = "Санкт-Петербург",
                        CountryId = country.Id,
                    };
                model.Cities.Add(masterCity);
                var sateliteCity1 = new City
                {
                    Name = "Колпино",
                    CountryId = country.Id,
                    SatelliteTo = masterCity
                };
                model.Cities.Add(sateliteCity1);
                var sateliteCity2 = new City
                {
                    Name = "Пулково",
                    CountryId = country.Id,
                    SatelliteTo = masterCity
                };
                model.Cities.Add(sateliteCity2);
                model.SaveChanges();
                masterCity = model.Cities.FirstOrDefault(x => x.Name == "Санкт-Петербург");
                Assert.IsNotNull(masterCity, "Master city should be present");
                Assert.AreEqual(2, masterCity.Satellites.Count, "There should be 2 satellites");
            }
        }

        [TestMethod]
        public void Showtimes()
        {
            var uri = new Uri("http://www.afisha.ru/abakan/changecity/");
            Console.WriteLine(uri.AbsolutePath);

            var cinema = new Cinema
            {
                Name = "Name",
                CityId = 1,
                CreationDate = DateTime.Now,
            };

            var showtimeKinopoisk = new Showtime
            {
                MovieId = 1,
                CinemaId = 2,
                SessionsRaw = "10:00; 11:00; 12:00; 13:00",
                SessionsCount = 4,
                SessionsFormat = SessionFormat.TwoD.ToString(),
                TargetSite = "kinopoisk.ru".GetTargetSite(),
                Date = DateTime.Today,
                CreationDate = DateTime.Now
            };

            var showtimeAfisha = new Showtime
            {
                MovieId = 1,
                CinemaId = 2,
                SessionsRaw = "10:00; 11:00",
                SessionsCount = 2,
                SessionsFormat = SessionFormat.FourDX.ToString(),
                TargetSite = "afisha.ru".GetTargetSite(),
                Date = DateTime.Today,
                CreationDate = DateTime.Now
            };
            var showtimeKinopoisk2 = new Showtime
            {
                MovieId = 1,
                CinemaId = 2,
                SessionsRaw = "10:00; 11:00",
                SessionsCount = 2,
                SessionsFormat = SessionFormat.TwoD.ToString(),
                TargetSite = "kinopoisk.ru".GetTargetSite(),
                Date = DateTime.Today,
                CreationDate = DateTime.Now
            };
            using (var model = new MovieScheduleStatsEntities())
            {
                //model.Cinemas.Add(cinema);
                model.SaveChanges();
                model.Showtimes.Add(showtimeKinopoisk);
                model.SaveChanges();
                showtimeAfisha = ParsingHelper.CheckShowtimePresent(showtimeAfisha, model);
                if (showtimeAfisha != null)
                    model.Showtimes.Add(showtimeAfisha);
                model.SaveChanges();
                showtimeKinopoisk2 = ParsingHelper.CheckShowtimePresent(showtimeKinopoisk2, model);
                if (showtimeKinopoisk2 != null)
                    model.Showtimes.Add(showtimeKinopoisk2);
                model.SaveChanges();

            }
        }

        private Dictionary<string, List<string>> GetDict()
        {
            var source = new Dictionary<string, List<string>>();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();
                            if (source.Keys.Contains(city))
                                source[city].Add(cinema);
                            else
                                source.Add(city, new List<string> { cinema });
                        }
                    }
                }
            }
            return source;
        }

        [TestMethod]
        public void CheckExistingCinemas_Exclusion()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                //var cinemas = model.Cinemas.Where(x => x.City.Name == city && x.Name == cinema &&
                                //                                        x.Sources.Any(xx => xx.TargetSite == "afisha.ru"))
                                //                    .ToList();
                                //&& x.Sources.Any(xx => xx.TargetSite == "kinopoisk.ru")))

                                var values = source[city];

                                var cinemas = model.Cinemas.Where(x => x.City.Name == city && !values.Contains(x.Name)
                                    && x.Sources.Any(xx => xx.TargetSite == "afisha.ru")
                                    //&& x.Sources.Any(xx => xx.TargetSite == "kinopoisk.ru")
                                    ).Select(x => x.Name)
                                .ToList();

                                string ccc = string.Empty;
                                if (cinemas.Count > 0)
                                {
                                    if (lastCity != city)
                                    {
                                        lastCity = city;
                                        ccc = string.Join("; ", cinemas);
                                    }
                                }
                                if (string.IsNullOrEmpty(ccc))
                                    sb.AppendFormat(
                                        "<tr><td style='color:#BDBDBD'>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                        city, ccc);
                                else
                                {
                                    sb.AppendFormat("<tr><td>{0}</td><td>{1}</td></tr>\r\n", city, ccc);
                                }

                                //sb.AppendFormat("<tr><td><b>{0}</b></td><td><b>{1}</b></td></tr>\r\n", items[0], items[1]);

                                //else
                                //{
                                //    sb.AppendFormat("<tr><td style='color:#BDBDBD'>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n", items[0], items[1]);
                                //}
                            }
                        }
                    }
                }
            }

            //using (var model = new MovieScheduleStatsEntities())
            //{
            //    foreach (var s in source)
            //    {
            //        var cinemas = model.Cinemas.Where(x => x.City.Name == s.Key && !s.Value.Contains(x.Name)
            //            //&& x.Sources.All(xx => xx.TargetSite == "afisha.ru")))
            //            //&& x.Sources.All(xx => xx.TargetSite == "kinopoisk.ru")))

            //                                            //&& x.Sources.Any(xx => xx.TargetSite == "afisha.ru")
            //            //&& x.Sources.Any(xx => xx.TargetSite == "kinopoisk.ru")))

            //                                            &&
            //                                            x.Sources.Any(xx => xx.TargetSite == "afisha.ru")).Select(x => x.Name)
            //                        .ToList();
            //        //&& x.Sources.Any(xx => xx.TargetSite == "kinopoisk.ru")))
            //        if (cinemas.Count > 0)
            //        {
            //            sb.AppendFormat("<tr><td><b>{0}</b></td><td><b>{1}</b></td></tr>\r\n", s.Key, string.Join("; ", cinemas));
            //        }
            //        else
            //        {
            //            sb.AppendFormat("<tr><td style='color:#BDBDBD'>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n", items[0], items[1]);
            //        }
            //    }

            //}
            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result.txt", sb.ToString());
        }

        [TestMethod]
        public void CheckExistingCinemas_Inclusion_All()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                var cinemaFound = model.Cinemas.Any(x => x.City.Name == city && x.Name == cinema
                                                                     &&
                                                                     x.Sources.Any(xx => xx.TargetSite == "afisha.ru")
                                                                     &&
                                                                     x.Sources.Any(xx => xx.TargetSite == "kinopoisk.ru"));


                                if (cinemaFound)
                                {
                                    sb.AppendFormat(
                                        "<tr><td>{0}</td><td><b>{1}</b></td></tr>\r\n",
                                        city, cinema);
                                }
                                else
                                {
                                    sb.AppendFormat(
                                            "<tr><td>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                            city, cinema);
                                }

                            }
                        }
                    }
                }
            }

            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result_inclusion_all.txt", sb.ToString());
        }

        [TestMethod]
        public void CheckExistingCinemas_Inclusion_KP()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                var cinemaFound = model.Cinemas.Any(x => x.City.Name == city && x.Name == cinema
                                                                     &&
                                                                     x.Sources.All(xx => xx.TargetSite == "kinopoisk.ru"));


                                if (cinemaFound)
                                {
                                    sb.AppendFormat(
                                        "<tr><td>{0}</td><td><b>{1}</b></td></tr>\r\n",
                                        city, cinema);
                                }
                                else
                                {
                                    sb.AppendFormat(
                                            "<tr><td>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                            city, cinema);
                                }

                            }
                        }
                    }
                }
            }

            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result_inclusion_kinopoisk.txt", sb.ToString());
        }

        [TestMethod]
        public void CheckExistingCinemas_Inclusion_AF()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            var source = GetDict();
            using (var fs = new FileStream(MasterListSource, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    string lastCity = string.Empty;
                    while (sr.Peek() != -1)
                    {
                        var line = sr.ReadLine();
                        if (line != null)
                        {
                            var items = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var city = items[0].Trim();
                            var cinema = items[1].Trim();

                            using (var model = new MovieScheduleStatsEntities())
                            {

                                var cinemaFound = model.Cinemas.Any(x => x.City.Name == city && x.Name == cinema
                                                                     &&
                                                                     x.Sources.All(xx => xx.TargetSite == "afisha.ru"));


                                if (cinemaFound)
                                {
                                    sb.AppendFormat(
                                        "<tr><td>{0}</td><td><b>{1}</b></td></tr>\r\n",
                                        city, cinema);
                                }
                                else
                                {
                                    sb.AppendFormat(
                                            "<tr><td>{0}</td><td style='color:#BDBDBD'>{1}</td></tr>\r\n",
                                            city, cinema);
                                }

                            }
                        }
                    }
                }
            }

            sb.AppendLine("</table>");
            File.WriteAllText(@"C:\result_inclusion_afisha.txt", sb.ToString());
        }


        //private string _masterListFile = @"c:\Users\mpak\Dropbox\MovieSchedule\Documents\master-list+kp+afisha-updated.xlsx";
        private string _masterListFile = @"c:\Users\mikachi\Dropbox\MovieSchedule\Documents\master-list+kp+afisha-updated.xlsx";
        //private string _masterListFile = @"c:\Users\mikachi\Dropbox\MovieSchedule\Documents\master-list+kp+afisha-updated.xlsx";

        [TestMethod]
        public void ParseMasterListEPP()
        {
            Dictionary<string, int> cinemas = DataHelper.ParseMasterList(_masterListFile);

            foreach (var cinema in cinemas.Where(x => x.Value > 1))
            {
                Console.WriteLine("{0} - {1}", cinema.Key, cinema.Value);
            }
        }

        [TestMethod]
        public void ParseMasterListExcel()
        {
            var sheets = Workbook.Worksheets(_masterListFile);

            foreach (var worksheet in sheets)
            {
                foreach (var row in worksheet.Rows)
                {
                    foreach (var cell in row.Cells)
                    {
                        var t = cell.Text;
                    }
                }
            }
        }

        [TestMethod]
        public void LoadCityTest()
        {
            using (var model = new MovieScheduleStatsEntities())
            {
                var city = model.Cities.FirstOrDefault(x => x.Name == "Ахтубинск");
                Assert.IsNotNull(city);
                Assert.IsNotNull(city.Sources);
            }
        }

        class CityFederalDistrict
        {
            internal string City { get; set; }
            internal string FederalDistrict { get; set; }
        }

        [TestMethod]
        public void LoadFederalDistrictsTest()
        {
            var csvPath = @"C:/Users/mikachi/Desktop/fo_cities.csv";
            var federalDistrictCitites = new Dictionary<string, List<string>>();
            //var citiesFederalDistrict = new Dictionary<string, string>();
            var citiesFederalDistrict = new List<CityFederalDistrict>();
            using (var fs = new FileStream(csvPath, FileMode.Open))
            {
                using (var sr = new StreamReader(fs))
                {
                    while (sr.Peek() > 0)
                    {
                        var line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            Console.WriteLine("Empty line");
                            continue;
                        }
                        var array = line.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (array.Length == 0 || array.Length != 3)
                        {
                            Console.WriteLine(line);
                            Console.WriteLine("Inconsistent array");
                            continue;
                        }
                        var city = array[0];
                        var federalDistrict = array[2];
                        federalDistrictCitites.AddOrUpdate(federalDistrict, new List<string> { city });
                        citiesFederalDistrict.Add(new CityFederalDistrict { City = city.Replace("ё", "е").Replace("Ё", "Е"), FederalDistrict = federalDistrict });
                    }
                    int foundCounts = 0;
                    using (var model = new MovieScheduleStatsEntities())
                    {
                        var federalDistricts = new List<FederalDistrict>();
                        foreach (var kv in federalDistrictCitites.Keys)
                        {
                            var fd = new FederalDistrict { Name = kv };
                            federalDistricts.Add(fd);
                            model.FederalDistricts.Add(fd);
                        }
                        model.SaveChanges();

                        int count = 0;
                        foreach (var cfd in citiesFederalDistrict)
                        {
                            var storedCity = model.Cities.FirstOrDefault(xx => xx.Name == cfd.City);
                            if (storedCity != null)
                            {
                                Console.WriteLine("UPDATE [dbo].[City] SET [FederalDistrictId] = {0} WHERE Id = {1}", federalDistricts.Find(x => x.Name == cfd.FederalDistrict).Id, storedCity.Id);
                                count++;
                            }
                        }
                        foundCounts += count;
                    }
                    Console.WriteLine("{0} cities found, {1} cities matched", citiesFederalDistrict.Count, foundCounts);
                }
            }
        }

        [TestMethod]
        public void CheckVolgaFilms()
        {
            string masterListFile = @"D:/Hunger_Games_Part1.xlsx";
            using (ExcelPackage pck = new ExcelPackage(new FileInfo(masterListFile)))
            {
                using (var model = new MovieScheduleStatsEntities())
                {

                    int cityIndex = 4;
                    int cinemaIndex = 5;
                    int foundCount = 0;
                    int nonStrictMatchCount = 0;
                    foreach (var worksheet in pck.Workbook.Worksheets)
                    {
                        int columnsCount = worksheet.Dimension.End.Column + 1;
                        int rowsCount = worksheet.Dimension.End.Row + 1;

                        for (int i = 3; i < rowsCount; i++)
                        {

                            string city = GetCellValue(worksheet, i, cityIndex);
                            string cinema = GetCellValue(worksheet, i, cinemaIndex);

                            if (
                                model.Cinemas.Any(x => x.Name == cinema &&
                                    (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Showtimes.Any())
                                || model.Cinemas.Any(x => (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Sources.Any(xx => xx.Text == cinema) && x.Showtimes.Any())
                                )
                            {
                                foundCount++;
                                worksheet.Cells[i, cityIndex].Style.Font.Bold = true;
                                worksheet.Cells[i, cinemaIndex].Style.Font.Bold = true;
                                worksheet.Row(i).Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheet.Row(i).Style.Fill.PatternColor.SetColor(Color.Yellow);
                                worksheet.Row(i).Style.Fill.BackgroundColor.SetColor(Color.Yellow);
                            }
                            else
                            {
                                var cnm =
                                    model.Cinemas.FirstOrDefault(x => x.Name.Contains(cinema) && (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Showtimes.Any()) ??
                                    model.Cinemas.FirstOrDefault(x => (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Sources.Any(xx => xx.Text.Contains(cinema)) && x.Showtimes.Any());
                                if (cnm == null)
                                {
                                    cnm = model.Cinemas.FirstOrDefault(x => cinema.Contains(x.Name) && (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Showtimes.Any()) ??
                                    model.Cinemas.FirstOrDefault(x => (x.City.Name == city ||
                                    (x.City.SatelliteTo != null && x.City.SatelliteTo.Name == city)) && x.Sources.Any(xx => cinema.Contains(xx.Text)) && x.Showtimes.Any());
                                    if (cnm != null)
                                        Console.WriteLine("Reverse search");
                                }

                                if (cnm != null)
                                {
                                    nonStrictMatchCount++;
                                    Console.WriteLine("{0} | {1} | {2}", city, cinema, cnm.Name);
                                    foundCount++;
                                    worksheet.Cells[i, cityIndex].Style.Font.Bold = true;
                                    worksheet.Cells[i, cinemaIndex].Style.Font.Bold = true;
                                    worksheet.Row(i).Style.Fill.PatternType = ExcelFillStyle.Solid;
                                    worksheet.Row(i).Style.Fill.PatternColor.SetColor(Color.Yellow);
                                    worksheet.Row(i).Style.Fill.BackgroundColor.SetColor(Color.Yellow);
                                }
                            }
                        }
                    }
                    pck.Save();
                    Console.WriteLine("{0} ({1})", foundCount, nonStrictMatchCount);
                }
            }
        }

        private static string GetCellValue(ExcelWorksheet worksheet, int i, int j)
        {
            var cell = worksheet.Cells[i, j];

            return cell.Text.Replace("\"\"\"", string.Empty)
                .Replace("ё", "е").Replace("Ё", "Е")
                .Trim(' ', ';');
        }
    }
}
