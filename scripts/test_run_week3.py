"""Configuration precedence regressions; only synthetic credentials are used."""
import unittest
import importlib.util
from pathlib import Path
spec = importlib.util.spec_from_file_location("week3_launcher", Path(__file__).with_name("run-week3.py"))
launcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(launcher)
build_environment = launcher.build_environment


class LauncherConfigurationTests(unittest.TestCase):
    def test_user_secrets_are_not_shadowed_by_local_credentials(self):
        env = build_environment({"Auth__Operators__0__Password": "local"}, {},
                                {"Auth:Operators:0:Password": "configured"})
        self.assertNotIn("Auth__Operators__0__Password", env)

    def test_missing_odoo_and_jwt_still_use_local_fallback(self):
        env = build_environment({"Odoo__ApiKey": "local-api", "Jwt__Key": "local-signing"}, {}, {})
        self.assertEqual("local-api", env["Odoo__ApiKey"])
        self.assertEqual("local-signing", env["Jwt__Key"])

    def test_explicit_environment_has_highest_priority_case_insensitively(self):
        env = build_environment({"Jwt__Key": "local"}, {"JWT__KEY": "explicit"}, {"Jwt:Key": "secret"})
        self.assertEqual("explicit", env["JWT__KEY"])
        self.assertNotIn("Jwt__Key", env)

    def test_nested_operator_secrets_are_recognized(self):
        env = build_environment({"Auth__Operators__0__Password": "local"}, {},
                                {"Auth": {"Operators": [{"Password": "configured"}]}})
        self.assertNotIn("Auth__Operators__0__Password", env)

    def test_non_development_does_not_assume_user_secrets_are_loaded(self):
        env = build_environment({"Jwt__Key": "local"}, {"DOTNET_ENVIRONMENT": "Production"}, {"Jwt:Key": "secret"})
        self.assertEqual("local", env["Jwt__Key"])

    def test_admin_password_never_becomes_process_configuration(self):
        self.assertNotIn("DevelopmentAdminPassword", build_environment({"DevelopmentAdminPassword": "local"}, {}, {}))


if __name__ == "__main__":
    unittest.main()
