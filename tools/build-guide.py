"""Generate the PDF guide from the repository's installation Markdown."""
from pathlib import Path
from html import escape
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer

root = Path(__file__).resolve().parents[1]
styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name='GuideTitle', fontName='Helvetica-Bold', fontSize=25, leading=30, textColor=colors.HexColor('#342d7e'), spaceAfter=18))
styles.add(ParagraphStyle(name='GuideBody', fontName='Helvetica', fontSize=10.5, leading=15, spaceAfter=10, alignment=TA_LEFT))
styles['Heading2'].textColor = colors.HexColor('#342d7e')
styles['Heading2'].spaceBefore = 13
styles['Heading2'].spaceAfter = 8
story = []
for block in (root / 'docs/INSTALACION.md').read_text(encoding='utf-8').split('\n\n'):
    block = block.strip()
    if not block:
        continue
    style = styles['GuideBody']
    if block.startswith('# '):
        style, block = styles['GuideTitle'], block[2:]
    elif block.startswith('## '):
        style, block = styles['Heading2'], block[3:]
    story.append(Paragraph(escape(block).replace('\n', '<br/>'), style))

def footer(canvas, doc):
    canvas.setStrokeColor(colors.HexColor('#dedde8'))
    canvas.line(42, 40, A4[0]-42, 40)
    canvas.setFont('Helvetica', 8)
    canvas.setFillColor(colors.HexColor('#626071'))
    canvas.drawString(42, 27, 'Codex Usage Monitor | 0.5.0 beta | MIT')
    canvas.drawRightString(A4[0]-42, 27, str(doc.page))

SimpleDocTemplate(str(root/'docs/Guia-instalacion.pdf'), pagesize=A4,
                 rightMargin=42, leftMargin=42, topMargin=40, bottomMargin=58,
                 title='Codex Usage Monitor - Guia de instalacion', author='jchaer-dot').build(story, onFirstPage=footer, onLaterPages=footer)
