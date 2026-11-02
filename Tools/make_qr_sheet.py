"""
Makes a printable PDF of QR codes, one per map checkpoint.

Usage:
    pip install qrcode reportlab
    python Tools/make_qr_sheet.py Assets/StreamingAssets/Maps/pup_main.json qr_sheet.pdf

Each page = one checkpoint: big QR code + spot name + "point the arrow this way" hint.
Print, laminate, and stick it at the spot facing the direction in the map file.
"""
import json
import sys

import qrcode
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas

MAROON = (0.47, 0.0, 0.0)


def main(map_path: str, out_path: str) -> None:
    with open(map_path, encoding="utf-8") as f:
        data = json.load(f)

    campus = data["campusId"]
    pdf = canvas.Canvas(out_path, pagesize=A4)
    w, h = A4

    for node in data["nodes"]:
        payload = f"WAYFIND:1:{campus}:{node['id']}"
        img = qrcode.make(payload, error_correction=qrcode.constants.ERROR_CORRECT_H, border=2)
        size = 140 * mm

        pdf.setFillColorRGB(*MAROON)
        pdf.rect(0, h - 40 * mm, w, 40 * mm, fill=1, stroke=0)
        pdf.setFillColorRGB(1, 1, 1)
        pdf.setFont("Helvetica-Bold", 30)
        pdf.drawCentredString(w / 2, h - 22 * mm, "Scan for directions")
        pdf.setFont("Helvetica", 14)
        pdf.drawCentredString(w / 2, h - 32 * mm, data.get("campusName", campus))

        pdf.drawImage(ImageReader(img.get_image()), (w - size) / 2, h - 50 * mm - size, size, size)

        pdf.setFillColorRGB(0, 0, 0)
        pdf.setFont("Helvetica-Bold", 28)
        pdf.drawCentredString(w / 2, h - 65 * mm - size, node["name"])
        pdf.setFont("Helvetica", 11)
        pdf.setFillColorRGB(0.4, 0.4, 0.4)
        pdf.drawCentredString(w / 2, h - 75 * mm - size,
                              f"Checkpoint: {node['id']}   |   Mount facing {node.get('facing', 0):.0f} deg from north")
        pdf.drawCentredString(w / 2, 15 * mm, payload)
        pdf.showPage()

    pdf.save()
    print(f"Saved {len(data['nodes'])} QR codes to {out_path}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    main(sys.argv[1], sys.argv[2])
