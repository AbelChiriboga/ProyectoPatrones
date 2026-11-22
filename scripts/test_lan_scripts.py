#!/usr/bin/env python3
"""
test_lan_scripts.py
Pruebas automatizadas unitarias para los scripts de red local (detect_lan_ip.py y verify_lan.py).
"""

import os
import re
import unittest
from detect_lan_ip import get_lan_ip
from verify_lan import check_tcp_port, check_http_endpoint, check_cors_preflight, run_lan_verification


class LanScriptsTests(unittest.TestCase):
    def test_get_lan_ip_returns_valid_ipv4_format(self):
        ip = get_lan_ip()
        self.assertIsInstance(ip, str)
        self.assertTrue(len(ip) >= 7)
        pattern = r"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"
        self.assertRegex(ip, pattern)

    def test_check_tcp_port_closed_handles_gracefully(self):
        # El puerto 59998 difícilmente estará abierto en un host de prueba
        res = check_tcp_port("127.0.0.1", 59998, timeout_sec=0.5)
        self.assertEqual("FAIL", res["status"])
        self.assertFalse(res["open"])
        self.assertIsNotNone(res["error"])
        self.assertGreaterEqual(res["latency_ms"], 0)

    def test_check_http_endpoint_unreachable_handles_gracefully(self):
        res = check_http_endpoint("http://127.0.0.1:59998/test", timeout_sec=0.5)
        self.assertEqual("FAIL", res["status"])
        self.assertEqual(0, res["http_status"])
        self.assertIsNotNone(res["error"])

    def test_check_cors_preflight_closed_returns_not_passed(self):
        res = check_cors_preflight("http://127.0.0.1:59998", "http://192.168.1.50:5173")
        self.assertFalse(res["cors_passed"])
        self.assertEqual("FAIL", res["status"])

    def test_run_lan_verification_generates_valid_structure(self):
        report_path = os.path.join("docs", "evidencias", "temp_test_report.json")
        try:
            results = run_lan_verification(
                server_ip="127.0.0.1",
                client_ip="192.168.1.100",
                api_port=59998,
                ui_port=59997,
                odoo_port=59996,
                report_file=report_path
            )
            self.assertIn("timestamp", results)
            self.assertEqual("127.0.0.1", results["server_ip"])
            self.assertEqual("192.168.1.100", results["client_ip"])
            self.assertIn("ports", results)
            self.assertIn("http_checks", results)
            self.assertIn("overall_status", results)
            self.assertTrue(os.path.exists(report_path))
        finally:
            if os.path.exists(report_path):
                os.remove(report_path)


if __name__ == "__main__":
    unittest.main()
