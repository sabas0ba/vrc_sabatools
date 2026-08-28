#!/usr/bin/env python3
"""Report NUnit test cases from a Unity Test Framework result file.

The command fails when Unity wrote no test cases or when any case did not
pass. It uses only the Python standard library and is intended to run through
the repository's pinned Python container.

Usage: check_unity_results.py <results.xml>
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", help="Unity Test Framework NUnit XML")
    args = parser.parse_args()

    try:
        root = ET.parse(args.results).getroot()
    except (OSError, ET.ParseError) as error:
        print(f"error: could not read {args.results}: {error}", file=sys.stderr)
        return 1

    cases = list(root.iter("test-case"))
    failed = 0

    for case in cases:
        result = case.get("result", "Unknown")
        print(f"  [{result:<7}] {case.get('fullname', '<unnamed>')}")
        if result == "Passed":
            continue

        failed += 1
        message = case.find("failure/message")
        if message is not None and message.text:
            print("      " + message.text.strip()[:1000])

    if not cases:
        print("error: the Unity result contains no test cases", file=sys.stderr)
        return 1

    print(
        f"total={root.get('total', len(cases))} "
        f"passed={root.get('passed', len(cases) - failed)} "
        f"failed={root.get('failed', failed)}"
    )
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
