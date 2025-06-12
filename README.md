Here’s a clean and professional `README.md` for your project:

---

# 🛍️ Retail Store Synthetic Dataset Generator

This project focuses on generating a **high-quality synthetic dataset** using **Unity** to simulate a realistic 3D retail store environment. The goal is to improve computer vision model performance in retail scenarios—such as inventory tracking and theft detection—by capturing diverse visual conditions not easily found in real-world datasets.

---

## 🎯 Project Overview

Using Unity, I’m simulating a retail environment equipped with strategically placed **virtual IP cameras**. These cameras generate synthetic image data under various:

* Lighting conditions
* Product arrangements
* Store layouts

This simulation aims to boost model generalization and performance when combined with real-world footage.

---

## 🛠️ Challenges & Solutions

### 1️⃣ Viewport-Centered Labeling

**Problem:**
Labels were originally assigned based on the object's spatial distance from the camera, often labeling items not clearly visible.

**Solution:**
Implemented viewport-center prioritization by calculating object proximity to the **camera's center view**, ensuring only clearly visible items are labeled.

---

### 2️⃣ Overlapping Bounding Boxes

**Problem:**
Overlapping objects produced dense, redundant bounding boxes that degraded model performance during training.

**Solution:**
Integrated **Intersection-over-Union (IoU)** filtering to remove redundant or highly overlapping bounding boxes, preserving only the most relevant annotations.

---

## 📈 What's Next

The next phase is to:

* **Blend synthetic data** with real camera footage from a local retail store
* Ensure diverse coverage of real-world edge cases
* Fine-tune object detection and behavior recognition models (e.g., YOLOv8n, PoseLift-based theft detection)

---

## 🤝 Contributing & Feedback

I’m open to collaboration and feedback!
If you’ve worked with synthetic data or applied AI in retail settings, feel free to connect or share insights.

---

## 🔧 Tools & Technologies

* **Unity** (for 3D simulation and synthetic frame generation)
* **Python** (for bounding box filtering and dataset processing)
* **YOLOv8n** (for object detection model fine-tuning)
* **OpenCV** and **NumPy** (for image and IoU operations)

---

## 📬 Contact

**Arashdeep Mehroke**
Feel free to reach out via [LinkedIn](https://www.linkedin.com/in/arashdeep-singh-020398251/)) or message me directly for project details or collaboration opportunities.

---

Let me know if you want it adapted for GitHub Pages, with demo visuals, dataset links, or model performance charts.
