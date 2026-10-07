import re

with open("medlink_lab_frontend/run_prototype.html", "r", encoding="utf-8") as f:
    text = f.read()

views = re.findall(r'currentView\s*===?\s*[\'"]([^\'"]+)[\'"]', text)
print('Current views:', sorted(list(set(views))))

steps = re.findall(r'pipelineStep\s*===?\s*([0-9]+)', text)
print('Pipeline steps:', sorted(list(set(steps))))

items = re.findall(r'<q-item[^>]*@click=["\']currentView\s*=\s*[\'"]([^\'"]+)[\'"]', text)
print('Nav items to views:', sorted(list(set(items))))
